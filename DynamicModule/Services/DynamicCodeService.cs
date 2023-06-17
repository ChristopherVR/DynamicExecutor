using DynamicModule.Attributes;
using DynamicModule.Context;
using DynamicModule.Enums;
using DynamicModule.Exceptions;
using DynamicModule.Extensions;
using DynamicModule.Infrastructure;
using DynamicModule.Infrastructure.Interfaces;
using DynamicModule.Models;
using DynamicModule.Options;
using DynamicModule.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TypeInfo = System.Reflection.TypeInfo;

namespace DynamicModule.Services;

/// <summary>
/// Implementation for the <see cref="IDynamicCodeService"/> interface.
/// Uses Roslyn API to generate code into CSharp using the Microsoft.CodeAnalysis.CSharp library.
/// See <code>https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp</code>
/// </summary>
public sealed class DynamicCodeService : IDynamicCodeService
{
    private byte[]? _generatedCode;
    private readonly ILogger _logger;
    private readonly IHttpContextAccessor _context;
    private readonly IServiceScope _serviceProvider;
    private readonly IAnalyseCodeService _analyseCodeService;
    private readonly FileExportOptions? _fileExportOptions;

    public DynamicCodeService(
        ILogger logger,
        IHttpContextAccessor httpContextAccessor,
        IServiceScope serviceScope,
        IAnalyseCodeService analyseCodeService,
        IOptions<FileExportOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
        _analyseCodeService = analyseCodeService ?? throw new ArgumentNullException(nameof(analyseCodeService));
        _fileExportOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    #region Helpers
    private List<MetadataReference> GetMetadataReferences() => new(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => a.Location)
            .Select(r => MetadataReference.CreateFromFile(r))
            .ToArray()
        .Concat(Assembly.GetEntryAssembly() is null ? Enumerable.Empty<MetadataReference>() : new List<MetadataReference>(Assembly
            .GetEntryAssembly()!
            .GetReferencedAssemblies()
            .Where(a => !string.IsNullOrWhiteSpace(Assembly.Load(a).Location))
            .Select(y => MetadataReference.CreateFromFile(Assembly.Load(y).Location))) { MetadataReference.CreateFromFile(Assembly.GetEntryAssembly()!.Location) }))
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
        // Default netstandard assembly is required.
        MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0").Location),
        MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.1.0.0").Location),
        MetadataReference.CreateFromFile(Assembly.GetCallingAssembly().Location),
        MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
    };

    /// <summary>
    /// Maps the <see cref="DynamicCodeConfig"/> class that is used internally.
    /// </summary>
    /// <param name="sourceCode"></param>
    /// <param name="cfg"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    private static Action<DynamicCodeConfig> GetCodeConfig(string sourceCode, Action<SourceCodeConfig> cfg, params object[] args) => (c) =>
    {
        var obj = new SourceCodeConfig();
        cfg.Invoke(obj);

        c.OutputDllName = obj.OutputDllName;
        foreach ((HttpClientHandler ClientHandler, ICollection<string> Assemblies) in obj.CustomAssemblies)
        {
            c.AddCustomAssemblies(Assemblies, ClientHandler);
        }

        if (obj.OptimizeLevel is OptimizationLevel.Debug)
        {
            c.SetOptimizeToDebugMode();
        }

        c.ExecuteAsConsoleApplication = obj.ExecuteAsConsoleApplication;
        c.UnloadAssembly = obj.UnloadAssembly;

        c.RunCodeAnalysis = obj.RunCodeAnalysis;
        c.StoreCodeAnalysis = obj.StoreCodeAnalysis;

        c.SaveGeneratedCode = obj.SaveGeneratedCode;
        c.TreatWarningsAsErrors = obj.TreatWarningsAsErrors;

        c.AllowUnsafe = obj.AllowUnsafe;
        c.IgnoreErrors = obj.IgnoreErrors;

        c.NullableContextOptions = obj.NullableContextOptions;
        c.LoadExistingAssemblies = obj.LoadExistingAssemblies;

        foreach (var item in obj.ExistingAssembliesToLoad)
        {
            c.ExistingAssembliesToLoad.Add(item);
        }

        c.AddSourceFiles(sourceCode);
        c.SetArguments(args);
    };

    /// <summary>
    /// Returns a <see cref="List{MetadataReference}"/>. The <see cref="IDynamicCodeConfig.CustomAssemblies"/> is used alongside the static <see cref="GetMetadataReferences"/> field to build a list of assemblies required for the <see cref="SyntaxTree"/> list.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<List<MetadataReference>> GetAllAssembliesForSourceCodesAsync(IDynamicCodeConfig config, CancellationToken cancellationToken = default)
    {
        var references = GetMetadataReferences();
        foreach ((HttpClientHandler ClientHandler, ICollection<string> CustomAssemblies) in config.CustomAssemblies)
        {
            foreach (string assembly in CustomAssemblies)
            {
                try
                {
                    string xmlAssembly = await DownloadAssemblyAsync(assembly, ClientHandler, cancellationToken);
                    references.Add(MetadataReference.CreateFromFile(Assembly.Load(xmlAssembly).Location));
                }
                catch (Exception ex)
                {
                    // Don't break from loading this assembly.
                    _logger.LogError(ex, "An error occurred trying to load the custom assembly with Url {Url}. Generation will continue but might fail", assembly);
                }
            }
        }

        return references;
    }

    /// <summary>
    /// Downloads the custom assembl from the specified <see cref="Uri"/>.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="httpClientHandler">Custom client handler that is used if the <see cref="Uri"/> requires custom authentication.</param>
    /// <returns>An XML string.</returns>
    private static async Task<string> DownloadAssemblyAsync(string url, HttpClientHandler httpClientHandler, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(httpClientHandler, disposeHandler: false);
        string xml = await client.GetStringAsync(new Uri(url, UriKind.RelativeOrAbsolute), cancellationToken);
        return xml;
    }

    private static async Task<IEnumerable<string>> GetSourceCodesAsync(IDynamicCodeConfig cfg, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        IEnumerable<string> sourceCodes = Array.Empty<string>().Concat(cfg.SourceFiles);

        if (cfg.Files.Any())
        {
            foreach (IFormFile file in cfg.Files)
            {
                await file.CopyToAsync(ms, cancellationToken);
                sourceCodes = sourceCodes.Append(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
                ms.Seek(0, SeekOrigin.Begin);
            }
        }

        if (cfg.FolderPath is not null)
        {
            foreach (var path in Directory.EnumerateFiles(cfg.FolderPath).Where(y => !y.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var file = await File.ReadAllTextAsync(path, cancellationToken);
                sourceCodes = sourceCodes.Append(file);
            }
        }

        return sourceCodes;
    }

    /// <summary>
    /// Logs any warnings or errors emitted from the generated code and determines if code execution should continue based on if there is any errors.
    /// </summary>
    /// <param name="cfg"></param>
    /// <param name="result"></param>
    /// <exception cref="DynamicCodeException"></exception>
    private void RunDiagnostics(IDynamicCodeConfig cfg, EmitResult result)
    {
        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity is DiagnosticSeverity.Error);

        IEnumerable<Diagnostic> warnings = result.Diagnostics.Where(x => x.Severity is DiagnosticSeverity.Warning);

        if (!result.Success)
        {
            _logger.LogWarning("Compilation done with error.");

            foreach (Diagnostic diagnostic in failures)
            {
                _logger.LogError("Error: {Id} : {Message}", diagnostic.Id, diagnostic.GetMessage());
            }

            throw new DynamicCodeException("Source code generated with errors. See Information log for more info.");
        }

        foreach (Diagnostic warning in warnings)
        {
            _logger.LogWarning("Warning: {Id}: {Message}", warning.Id, warning.GetMessage());
        }

        if (cfg.TreatWarningsAsErrors && warnings.Any())
        {
            throw new DynamicCodeException("Warnings detected in compiled code. Resolve the warnings before executing this code.");
        }

        if (cfg.OutputAnalysisResult is not null)
        {
            cfg.OutputAnalysisResult(new CodeAnalysisResult(
                warnings.Select(warning => $"Warning: {warning.Id}: {warning.GetMessage()}"),
                failures.Select(failure => $"Error: {failure.Id}: {failure.GetMessage()}")));
        }

        _logger.LogDebug("Compilation done without any errors.");
    }

    #endregion Helpers

    /// <summary>
    /// Generates the dynamic code.
    /// </summary>
    /// <param name="cfg"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The assembly name loaded in memory.</returns>
    private async Task<string> GenerateCodeAsync(IDynamicCodeConfig cfg, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> rawLiterals = await GetSourceCodesAsync(cfg, cancellationToken);

        using var peStream = new MemoryStream();

        var (Compilation, AssemblyName) = await CompileCodeAsync(rawLiterals, cfg, cancellationToken);

        if (cfg.RunCodeAnalysis)
        {
            await _analyseCodeService.ValidateCodeAsync(Compilation, storeAnalysis: cfg.StoreCodeAnalysis, cancellationToken: cancellationToken);
        }

        EmitResult result = Compilation.Emit(peStream, cancellationToken: cancellationToken);

        RunDiagnostics(cfg, result);

        peStream.Seek(0, SeekOrigin.Begin);

        _generatedCode = peStream.ToArray();

        await peStream.DisposeAsync();

        return AssemblyName;
    }

    /// <summary>
    /// Compiles the source codes into a <see cref="CSharpCompilation"/> object and configuring the <see cref="CSharpCompilationOptions"/> using the <see cref="IDynamicCodeConfig"/> configuration.
    /// </summary>
    /// <param name="sourceCodes"></param>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Compiled CSharp code.</returns>
    private async Task<(CSharpCompilation Compilation, string AssemblyName)> CompileCodeAsync(IEnumerable<string> sourceCodes, IDynamicCodeConfig config, CancellationToken cancellationToken = default)
    {
        CSharpParseOptions options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var trees = sourceCodes
            .Select(f => CSharpSyntaxTree.ParseText(f, options))
            .ToList();

        var references = await GetAllAssembliesForSourceCodesAsync(config, cancellationToken);

        const string Dll = nameof(Dll);
        const string Exe = nameof(Exe);
        string assemblyName = config.OutputDllName is not null
            ? $"{config.OutputDllName}_{Guid.NewGuid().ToString()[8..]}.{(config.ExecuteAsConsoleApplication ? Exe.ToLower() : Dll.ToLower())}"
            : $"DynamicCode_{Guid.NewGuid().ToString()[8..]}.{(config.ExecuteAsConsoleApplication ? Exe.ToLower() : Dll.ToLower())}";

        var cSharpCompilationOptions = new CSharpCompilationOptions(config.ExecuteAsConsoleApplication ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary)
            .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
            .WithOptimizationLevel(config.OptimizeLevel)
            .WithAllowUnsafe(config.AllowUnsafe)
            .WithNullableContextOptions(config.NullableContextOptions)
            .WithPlatform(Platform.AnyCpu);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            references: references,
            options: cSharpCompilationOptions);

        return (Compilation: compilation, AssemblyName: assemblyName);
    }

    /// <inheritdoc/>
    public async Task<IList<InvocationResult>> ExecuteCodeAsync(Action<DynamicCodeConfig> config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        DynamicCodeConfig cfg = new();
        config.Invoke(cfg);

        // Todo: Ensure this assemblies are removed after use.
        _ = LoadExistingDynamicAssemblies(cfg.ExistingAssembliesToLoad);

        string assemblyName = await GenerateCodeAsync(cfg, cancellationToken);

        if (_generatedCode is null)
        {
            throw new DynamicCodeException($"An error ocurred trying to execute the generated code for assembly {assemblyName}.");
        }

        if (cfg.SaveGeneratedCode && _fileExportOptions is not null)
        {
            await File.WriteAllBytesAsync($"{_fileExportOptions.ExportPath}\\{assemblyName}", _generatedCode, cancellationToken);
        }

        (WeakReference Ref, List<InvocationResult> Result) = await LoadAndExecuteAssemblyAsync(_generatedCode, cfg, cancellationToken);

        // Forces the GC to cleanup dynamic DLL. Unloads it from memory.
        if (cfg.UnloadAssembly)
        {
            // Cleanup the memory
            for (int i = 0; i < 8 && Ref.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (Ref.IsAlive)
            {
                _logger.LogWarning("Unloading dynamic module failed for {Assembly}", assemblyName);
            }
            else
            {
                _logger.LogDebug("Dynamic module unloaded for {Assembly}", assemblyName);
            }
        }

        return Result;
    }

    /// <summary>
    /// Attempt to resolve the assembly if the assembly failed to load.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    private static Assembly? AssemblyResolve(object sender, ResolveEventArgs args)
            => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);

    /// <summary>
    /// Loads existing dynamic libraries compiled.
    /// </summary>
    /// <param name="exsitingAssembliesToLoad"></param>
    /// <returns></returns>
    private static List<string> LoadExistingDynamicAssemblies(IList<string> exsitingAssembliesToLoad)
    {
        var assembliesToUnload = new List<string>();
        foreach (var path in exsitingAssembliesToLoad)
        {
            string? assemblyName = Assembly.LoadFrom(path)?.FullName;
            if (assemblyName is not null)
            {
                assembliesToUnload.Add(assemblyName);
            }
        }

        return assembliesToUnload;
    }

    /// <summary>
    /// Loads the given <see cref="byte[]"/> into an <see cref="Assembly"/> object and executes it.
    /// </summary>
    /// <param name="compiledAssembly"></param>
    /// <param name="cfg"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The list of data received from the <see cref="Assembly"/>.</returns>
    private async Task<(WeakReference Ref, List<InvocationResult> Result)> LoadAndExecuteAssemblyAsync(
        byte[] compiledAssembly,
        IDynamicCodeConfig cfg,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading custom assembly.");
        using var asm = new MemoryStream(compiledAssembly);
        var assemblyLoadContext = new UnloadableAssemblyLoadContext();

        Assembly assembly = assemblyLoadContext.LoadFromStream(asm);
        MethodInfo? entry = assembly.EntryPoint;

        List<InvocationResult> result = new();

        // This local method is used numerous times only within this method and references scoped variables.
        async Task AddInvokedResultAsync(object? invokedEntry, Type returnType, InvokeType type)
        {
            if (invokedEntry is Task methodTask)
            {
                await methodTask.ConfigureAwait(false);

                if (returnType.IsGenericType)
                {
                    result.Add(new(returnType.FullName ?? returnType.Name, await (dynamic)invokedEntry));
                }
                else
                {
                    result.Add(new(returnType.FullName ?? returnType.Name, Unit.Task));
                }
            }
            else
            {
                result.Add(new(returnType.FullName ?? returnType.Name, returnType == typeof(void) ? Unit.Value : invokedEntry));
            }
        }

        // If this is a console app or static class that needs to be invoked, the EntryPoint will be populated.
        if (entry is not null)
        {
            object? invokedEntry = entry.Invoke(null, cfg.Arguments?.Length > 0 ? cfg.Arguments : entry.GetParameters().Select(y => GetParameterValue(y, assembly, cancellationToken)).ToArray());
            await AddInvokedResultAsync(invokedEntry, entry.ReturnType, InvokeType.EntryPoint);
        }

        // This local method is used numerous times only within this method and references scoped variables.
        async Task InvokeMethodsAsync(Type definedType, object? invokedObject)
        {
            // Filter out GetType, ToString, GetHashCode & Equals.
            // Virtual check will ensure we don't attempt to call any non-overrided methods
            // IsSpecialName check is done to prevent internal methods from returning. Ex. Record types returns a few methods declared internally to set & get properties.
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            static bool Predicate(MethodInfo y) => !y.IsVirtual && !y.IsSpecialName && y.GetCustomAttribute<CompilerGeneratedAttribute>() is null;

            foreach (MethodInfo method in definedType
                .GetMethods(flags)
                .Where(Predicate)
                .OrderBy(y => y.GetCustomAttribute<ExecuteOrderAttribute>()?.Order ?? default))
            {
                object?[] methodArgs = cfg.Arguments?.Length > 0 ? cfg.Arguments : method
                    .GetParameters()
                    .Select(y => GetParameterValue(y, assembly, cancellationToken))
                    .ToArray();

                try
                {
                    object? invokedMethod = method.Invoke(invokedObject, methodArgs);
                    await AddInvokedResultAsync(invokedMethod, method.ReturnType, InvokeType.Method);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred trying to invoke method {MethodName} on Assembly {AssemblyName}", method.Name, assembly.FullName);
                    if (!cfg.IgnoreErrors)
                    {
                        throw;
                    }
                    else
                    {
                        await AddInvokedResultAsync(null, method.ReturnType, InvokeType.Method);
                    }
                }
            }
        }

        // Execute any defined types in the source code ordered by the ExecuteOrder attribute.
        foreach (TypeInfo definedType in assembly.DefinedTypes
            .Where(y => y.IsClass && y.GetCustomAttribute<DontInvokeAttribute>() is null && !y.IsNotPublic && !y.Name.Contains("AnonymousType", StringComparison.OrdinalIgnoreCase))
            .OrderBy(y => y.GetCustomAttribute<ExecuteOrderAttribute>()?.Order ?? default))
        {
            ConstructorInfo? ctor = definedType.GetConstructors().FirstOrDefault(x => x.GetCustomAttribute<DontInvokeAttribute>() is null);
            ctor ??= definedType.GetConstructor(Type.EmptyTypes);

            if (ctor is not null)
            {
                object? invokedObject = default;
                try
                {
                    invokedObject = ctor?.Invoke(cfg.Arguments?.Length > 0 && ctor?.GetParameters()?.Length > 0 ? cfg.Arguments : ctor?.GetParameters().Select(y => GetParameterValue(y, assembly, cancellationToken)).ToArray());
                    invokedObject ??= FormatterServices.GetUninitializedObject(definedType);
                    // await AddInvokedResultAsync(invokedObject, definedType, InvokeType.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred trying to invoke Constructor {ConstructorName} on Assembly {AssemblyName}", ctor?.Name, assembly.FullName);
                    if (!cfg.IgnoreErrors)
                    {
                        throw;
                    }
                    else
                    {
                        await AddInvokedResultAsync(null, definedType, InvokeType.Method);
                    }
                }

                if (invokedObject is not null)
                {
                    await InvokeMethodsAsync(definedType, invokedObject);
                }
            }
            else
            {
                await InvokeMethodsAsync(definedType, null);
            }
        }

        // Unload the assembly.
        assemblyLoadContext.Unload();

        return (new WeakReference(assemblyLoadContext), result);
    }

    /// <summary>
    /// Gets the constructor or method paramater value. The server's <see cref="IServiceProvider"/> will be used to resolve any dependency injection services.
    /// <br/> If a <see cref="HttpContext"/> is available, this will be used to resolve any request data being posted alongside the generated method.
    /// </summary>
    /// <param name="propInfo">The <see cref="ParameterInfo"/> information for the given method or constructor.</param>
    /// <param name="assembly">The <see cref="Assembly"/> that will be used to determine if the <see cref="Type"/> exists within it.</param>
    /// <param name="token">Cancellation token that can be passed as the parameter value.</param>
    /// <returns></returns>
    private object? GetParameterValue(ParameterInfo propInfo, Assembly assembly, CancellationToken token)
    {
        IServiceProvider serviceProvider = (_context.HttpContext?.RequestServices ?? _serviceProvider.ServiceProvider)!;

        string? name = propInfo.Name?.ToUpperInvariant();
        if (serviceProvider.GetService(propInfo.ParameterType) is not null)
        {
            return serviceProvider.GetService(propInfo.ParameterType);
        }

        if (propInfo.ParameterType == typeof(ILogger<>)
            || propInfo.ParameterType.GetInterface(nameof(ILogger)) is not null)
        {
            using var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            return loggerFactory?.CreateGenericLogger(propInfo.ParameterType);
        }

        if (propInfo.ParameterType == typeof(CancellationToken))
        {
            return token;
        }

        if (TryGetReferencedTypeValue(propInfo, assembly, token, out var var))
        {
            return var;
        }

        if (propInfo.Name is not null && _context.HttpContext is not null)
        {
            var httpContext = _context.HttpContext;
            if (propInfo.GetCustomAttributes(typeof(FromFormAttribute), false).Any()
                   && httpContext.Request.HasFormContentType
                   && httpContext.Request.Form.Any(x => x.Key.ToUpperInvariant() == name))
            {
                return httpContext.Request.Form.TryGetValue(propInfo.Name, out var val) ? val : default;
            }

            if (httpContext.Request.Query.Any(x => x.Key.ToUpperInvariant() == name))
            {
                return Convert
                   .ChangeType(httpContext.Request.Query
                   .First(x => x.Key.ToUpperInvariant() == name).Value.ToString(), propInfo.ParameterType, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (httpContext.Request.Cookies.Any(x => x.Key.ToUpperInvariant() == name))
            {
                return httpContext.Request.Cookies
                   .First(x => x.Key.ToUpperInvariant() == name)
                   .Value
                   .ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (httpContext.Request.Headers.Any(x => x.Key.ToUpperInvariant() == name))
            {
                return httpContext.Request.Headers
                   .First(x => x.Key.ToUpperInvariant() == name)
                   .Value
                   .ToString();
            }

            if (propInfo.GetCustomAttributes(typeof(FromBodyAttribute), false).Any()
               && httpContext.Request.Body is not null)
            {
                using var sr = new StreamReader(httpContext.Request.Body!);
                string body = sr.ReadToEnd();
                object? data = System.Text.Json.JsonSerializer.Deserialize(body, propInfo.ParameterType);

                if (data is not null)
                {
                    return data;
                }
            }
        }

        if (!propInfo.ParameterType.IsValueType)
        {
            try
            {
                var instance = ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider.ServiceProvider, propInfo.ParameterType);
                return instance;
            }
            catch
            {
                _logger.LogDebug("Failed to get or create an instance for {Name}", propInfo.ParameterType.FullName);
            }
        }

        if (propInfo.HasDefaultValue)
        {
            return propInfo.DefaultValue;
        }

        if (propInfo.ParameterType.IsValueType)
        {
            return Activator.CreateInstance(propInfo.ParameterType);
        }

        // See if there is any current assembly loaded into the AppDomain that satisfies the type
        foreach (var ase in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryGetReferencedTypeValue(propInfo, ase, token, out var value))
            {
                return value;
            }
        }

        return default;
    }

    /// <summary>
    /// Tries to get a refenced value for the <see cref="ParameterInfo"/> in the specified <see cref="Assembly"/>.
    /// </summary>
    /// <param name="propInfo"></param>
    /// <param name="assembly"></param>
    /// <param name="token"></param>
    /// <param name="value"></param>
    /// <returns>True if successful.</returns>
    private bool TryGetReferencedTypeValue(ParameterInfo propInfo, Assembly assembly, CancellationToken token, out object? value)
    {
        // This is for cases where the dynamic code injects a service that is defined in the dynamic code.
        if (!propInfo.ParameterType.IsValueType)
        {
            try
            {
                Type? objectType = assembly.GetType(propInfo.ParameterType.FullName!, throwOnError: false, ignoreCase: true);
                if (objectType is not null)
                {
                    var constructors = objectType.GetConstructors();
                    // assume we will have only one constructor
                    var firstConstrutor = constructors.FirstOrDefault() ?? objectType.GetConstructor(Type.EmptyTypes);
                    var parameters = firstConstrutor?.GetParameters().Select(y => GetParameterValue(y, assembly, token)).ToArray();
                    value = firstConstrutor?.Invoke(parameters);
                    return true;
                }
            }
            catch
            {
                _logger.LogError("Failed to invoke type when attempted to find a reference for {PropInfo} in Assembly {Assembly}", propInfo.ParameterType.FullName, assembly.FullName);
            }
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public async Task ExecuteCodeAsync(string sourceCode, CancellationToken cancellationToken = default, params object[] args)
        => await ExecuteCodeAsync(c =>
        {
            c.AddSourceFiles(sourceCode);
            c.SetArguments(args);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<T> ExecuteCodeAsync<T>(string sourceCode, CancellationToken cancellationToken = default, params object[] args)
    {
        InvocationResult res = (await ExecuteCodeAsync(c =>
        {
        c.AddSourceFiles(sourceCode);
        c.SetArguments(args);
        }, cancellationToken)).First();

        if (res.Output is null)
        {
            return default!;
        }

        return res.Output.MapTo<T>();
    }

    /// <inheritdoc/>
    public async Task ExecuteCodeAsync(string sourceCode, Action<SourceCodeConfig> config, CancellationToken cancellationToken = default, params object[] args)
        => await ExecuteCodeAsync(GetCodeConfig(sourceCode, config, args), cancellationToken);

    /// <inheritdoc/>
    public async Task<T> ExecuteCodeAsync<T>(string sourceCode, Action<SourceCodeConfig> config, CancellationToken cancellationToken = default, params object[] args)
    {
        var result = (await ExecuteCodeAsync(GetCodeConfig(sourceCode, config, args), cancellationToken)).First();


        if (result.Output is null)
        {
            return default!;
        }

        return result.Output.MapTo<T>();
    }
    /// <inheritdoc/>
    public T ExecuteCode<T>(string sourceCode, params object[] args)
        => ExecuteCodeAsync<T>(sourceCode, CancellationToken.None, args).Result;

    /// <inheritdoc/>
    public IList<InvocationResult> ExecuteCode(Action<DynamicCodeConfig> config)
        => ExecuteCodeAsync(config).Result;

    /// <inheritdoc/>
    public void ExecuteCode(string sourceCode, Action<SourceCodeConfig> config, params object[] args)
        => ExecuteCodeAsync(sourceCode, config, CancellationToken.None, args).RunSynchronously();

    /// <inheritdoc/>
    public T ExecuteCode<T>(string sourceCode, Action<SourceCodeConfig> config, params object[] args)
        => ExecuteCodeAsync<T>(sourceCode, config, CancellationToken.None, args).Result;

    /// <inheritdoc/>
    public void ExecuteCode(string sourceCode, params object[] args)
        => ExecuteCodeAsync(sourceCode, CancellationToken.None, args).RunSynchronously();

    /// <inheritdoc/>
    public IList<T> ExecuteCode<T>(Action<DynamicCodeConfig> config)
        => ExecuteCode(config).Select(y => y.Output).MapTo<T>().ToList();

    /// <inheritdoc/>
    public async Task<IList<T>> ExecuteCodeAsync<T>(Action<DynamicCodeConfig> config, CancellationToken cancellationToken = default)
        => (await ExecuteCodeAsync(config, cancellationToken)).Select(y => y.Output).MapTo<T>().ToList();

}
