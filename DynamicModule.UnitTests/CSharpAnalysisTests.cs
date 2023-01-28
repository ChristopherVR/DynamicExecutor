using DynamicModule.Exceptions;
using DynamicModule.Extensions;
using DynamicModule.Infrastructure;
using DynamicModule.Models;
using DynamicModule.Options;
using DynamicModule.Services;
using DynamicModule.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace DynamicModule.UnitTests;
public class CSharpAnalysisTests
{
    private static string ExampleCodePath => Path.Combine(Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName ?? string.Empty, "ExampleCode");
    private readonly IHttpContextAccessor _httpContextAccesor;
    private readonly IServiceProvider _serviceProvider;
    public CSharpAnalysisTests()
    {
        var loggerFactory = new NullLoggerFactory();
        var defaultHttpContext = new DefaultHttpContext();
        _httpContextAccesor = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == defaultHttpContext);
        _serviceProvider = Mock.Of<IServiceProvider>(x => x.GetService(typeof(ILoggerFactory)) == loggerFactory);
    }

    /// <summary>
    /// This unit test will attempt to execute a console application (with custom values passed) and return no errors.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Execute_Console_App_Code_With_Arguments_And_Return_No_Errors()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        int res = await dynamicService.ExecuteCodeAsync<int>($@"using System; Console.WriteLine(""Hello world!""); return int.Parse(args[0]);", c =>
        {
            c.ExecuteAsConsoleApplication = true;
        }, cancellationToken: CancellationToken.None, new object[] { new string[] { "2" } });

        // Assert
        Assert.Equal(2, res);
    }

    /// <summary>
    /// This unit test will attempt to execute console application with Nullablities enabled. This is done to test whether warnings are
    /// being produced. An exception is thrown if warning are found in the code.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Execute_Console_App_Code_With_Nullability_Enable_Treat_Warnings_As_Errors_And_Throw_Exception()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var res = await Record.ExceptionAsync(() => dynamicService.ExecuteCodeAsync<int>($@"
            #nullable enable
            using System;
            Console.WriteLine(""Hello world!"");
            string test = null;
            Console.WriteLine(test);
            return 2;", c =>
        {
            c.ExecuteAsConsoleApplication = true;
            c.TreatWarningsAsErrors = true;
        }));

        var expectedMessage = "Warnings detected in compiled code. Resolve the warnings before executing this code.";

        // Assert
        Assert.IsType<DynamicCodeException>(res);
        Assert.Equal(expectedMessage, res.Message);
    }

    /// <summary>
    /// This unit test will execute code within a file that contains multiple methods.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Load_Sample_File_Should_Execute_Multiple_Methods_And_Return_No_Errors()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);

        var connString = "DbConnectionTest:ThisIsRandomCharacters.";

        var mockConfigurationSection = Mock.Of<IConfigurationSection>(x => x[It.IsAny<string>()] == connString);
        var mockConfiguration = Mock.Of<IConfiguration>(x => x.GetSection(It.IsAny<string>()) == mockConfigurationSection);
        var mockServiceProvider = Mock.Of<IServiceProvider>(x => x.GetService(typeof(IConfiguration)) == mockConfiguration
        && x.GetService(typeof(ILogger)) == NullLogger.Instance
        && x.GetService(typeof(ILoggerFactory)) == new NullLoggerFactory());
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == mockServiceProvider);

        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });

        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        var sourceCode = await File.ReadAllTextAsync(Path.Combine(ExampleCodePath, "ScenarioOne", "SampleCode.cs"));

        var expectedConnString = "DbConnectionTest:ThisIsRandomCharacters.";

        // Act
        IList<InvocationResult> res = await dynamicService.ExecuteCodeAsync(c =>
        {
            c.AddSourceFiles(sourceCode);
        });

        // Assert
        Assert.Equal(2, res.Count);
        Assert.Equal(expectedConnString, res[0].Output);
        Assert.Equal(Unit.Task, res[1].Output);
    }

    /// <summary>
    /// This unit test will execute sample file with multiple methods and will check whether the value being returned in the IConfiguration.GetConnectionString()
    /// doesn't match the expected value.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Load_Sample_File_Should_Execute_Multiple_Methods_With_Mismatch_Connection_String()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        var connString = "DbConnectionTest:ThisIsRandomCharacters.";

        var mockConfigurationSection = Mock.Of<IConfigurationSection>(x => x[It.IsAny<string>()] == connString);
        var mockConfiguration = Mock.Of<IConfiguration>(x => x.GetSection(It.IsAny<string>()) == mockConfigurationSection);
        var mockServiceProvider = Mock.Of<IServiceProvider>(x => x.GetService(typeof(IConfiguration)) == mockConfiguration && x.GetService(typeof(ILogger)) == NullLogger.Instance && x.GetService(typeof(ILoggerFactory)) == new NullLoggerFactory());
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == mockServiceProvider);

        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        var sourceCode = await File.ReadAllTextAsync(Path.Combine(ExampleCodePath, "ScenarioOne", "SampleCode.cs"));

        var expectedConnString = "DbConnectionTest:ThisIsRandomCharacterss.";

        // Act
        IList<InvocationResult> res = await dynamicService.ExecuteCodeAsync(c =>
        {
            c.AddSourceFiles(sourceCode);
        });

        // Assert
        Assert.Equal(2, res.Count);
        Assert.NotEqual(expectedConnString, res[0].Output);
        Assert.Equal(Unit.Task, res[1].Output);
    }

    /// <summary>
    /// This unit test will load code from a text file and will determine if the warnings count is the same than expected.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Load_Sample_Code_With_Warnings_Output_Warnings_Count()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IAnalyseCodeService codeService = new AnalyseCodeService(new NullLoggerFactory().CreateLogger<AnalyseCodeService>());
        var connString = "DbConnectionTest:ThisIsRandomCharacters.";

        var mockConfigurationSection = Mock.Of<IConfigurationSection>(x => x[It.IsAny<string>()] == connString);
        var mockConfiguration = Mock.Of<IConfiguration>(x => x.GetSection(It.IsAny<string>()) == mockConfigurationSection);
        var mockServiceProvider = Mock.Of<IServiceProvider>(x => x.GetService(typeof(IConfiguration)) == mockConfiguration
        && x.GetService(typeof(ILogger)) == NullLogger.Instance
        && x.GetService(typeof(ILoggerFactory)) == new NullLoggerFactory());
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == mockServiceProvider);

        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, codeService, mockOptions);

        var sourceCode = await File.ReadAllTextAsync(Path.Combine(ExampleCodePath, "ScenarioTwo", "SampleCodeWithWarnings.txt"));

        CodeAnalysisResult? analysis = default;
        // Act
        _ = await dynamicService.ExecuteCodeAsync(c =>
        {
            c.RunCodeAnalysis = true;
            c.OutputAnalysisResult = (c) => analysis = c;
            c.AddSourceFiles(sourceCode);
        });

        // Assert
        Assert.NotNull(analysis);
        Assert.NotEqual(1, analysis.Warnings.Count());
    }

    internal record ReturnType(bool FirstParam, int SecondParam, DateTime? ThirdParam);
    /// <summary>
    /// This unit test will execute code that returns an anonymous object.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_With_Multiple_Arguments_Return_Arguments_Success()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var res = await dynamicService.ExecuteCodeAsync<object>($@"
            using System;
            using System.Runtime.CompilerServices;
            [assembly:InternalsVisibleTo(""DynamicModule.UnitTests"")]
            // public record RecordTest(bool FirstParam, int SecondParam, DateTime? ThirdParam);
            public class Test
            {{
                //public RecordTest GetParamsBack(bool firstParam, int secondParam, DateTime? thirdParam)
                //{{
                //    return new RecordTest(firstParam, secondParam, thirdParam);
                //}}
                public object GetParamsBack(bool firstParam, int secondParam, DateTime? thirdParam)
                {{
                    return new
                    {{
                        FirstParam = firstParam,
                        SecondParam = secondParam,
                        ThirdParam = thirdParam,
                    }};
                }}
            }}", cancellationToken: CancellationToken.None, true, 2, new DateTime(2000));

        var output = res.MapObjectToDictionary();
        var expectedFirstParam = true;
        var expectedSecondParam = 2;
        var expectedThirdParam = new DateTime(2000);

        // Assert
        Assert.Equal(expectedFirstParam, output["FirstParam"]);
        Assert.Equal(expectedSecondParam, output["SecondParam"]);
        Assert.Equal(expectedThirdParam, output["ThirdParam"]);
    }

    /// <summary>
    /// This unit test will execute code taht contains multiple classes, but only one will be executed as the other is marked with the
    /// DontInvoke attribute.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_With_Multiple_Arguments_And_Sample_Code_Contains_Multiple_Dto_Classes_Only_Single_Class_Has_A_Method_And_Dont_Invoke_Attribute_To_Prevent_DTO_From_ExecutingReturn_Arguments_Success()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);

        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);

        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var res = await dynamicService.ExecuteCodeAsync<object>($@"
            using System;
            using System.Runtime.CompilerServices;
            [assembly:InternalsVisibleTo(""DynamicModule.UnitTests"")]
            [DynamicModule.Attributes.DontInvoke]
            public record RecordTest(bool FirstParam, int SecondParam, DateTime? ThirdParam);
            public class Test
            {{
                public RecordTest GetParamsBack(bool firstParam, int secondParam, DateTime? thirdParam)
                {{
                    return new RecordTest(firstParam, secondParam, thirdParam);
                }}
            }}", cancellationToken: CancellationToken.None, true, 2, new DateTime(2000));

        var output = res.MapObjectToDictionary();
        var expectedFirstParam = true;
        var expectedSecondParam = 2;
        var expectedThirdParam = new DateTime(2000);

        // Assert
        Assert.Equal(expectedFirstParam, output["FirstParam"]);
        Assert.Equal(expectedSecondParam, output["SecondParam"]);
        Assert.Equal(expectedThirdParam, output["ThirdParam"]);
    }

    /// <summary>
    /// This unit test will execute code that contains blacklisted namespaces and generates an exception
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Contains_Blacklisted_Namespaces_And_Uses_Dont_Invoke_Attribute_To_Prevent_DTO_From_Executing_Throw_Exception()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService codeService = new AnalyseCodeService(new NullLoggerFactory().CreateLogger<AnalyseCodeService>());
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, codeService, mockOptions);

        // Act
        var res = await Record.ExceptionAsync(() => dynamicService.ExecuteCodeAsync<object>($@"
            using System;
            using Microsoft.CodeAnalysis;
            using System.Runtime.CompilerServices;
            [assembly:InternalsVisibleTo(""DynamicModule.UnitTests"")]
            [DynamicModule.Attributes.DontInvoke]
            public record RecordTest(bool FirstParam, int SecondParam, DateTime? ThirdParam);
            public class Test
            {{
                public RecordTest GetParamsBack(bool firstParam, int secondParam, DateTime? thirdParam)
                {{
                    return new RecordTest(firstParam, secondParam, thirdParam);
                }}
            }}", config =>
           {
               config.RunCodeAnalysis = true;
           }, cancellationToken: CancellationToken.None, true, 2, new DateTime(2000)));

        var expectedMessage = "Declaration of namespace 'Microsoft.CodeAnalysis' is not allowed.";

        // Assert
        Assert.NotNull(res);
        Assert.IsType<DynamicCodeException>(res);
        Assert.Equal(expectedMessage, res.Message);
    }

    /// <summary>
    /// This unit test will execute code in a folder that contains references to classes within the code being generated.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Should_Execute_Multiple_Methods_Over_Multiple_Classes_In_Same_File_That_Injects_Each_Other_Sources_And_Return_No_Errors()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        IList<InvocationResult> res = await dynamicService.ExecuteCodeAsync(c =>
        {
            c.FolderPath = Path.Combine(ExampleCodePath, "ScenarioThree");
            c.TreatWarningsAsErrors = true;
        });

        // Assert
        Assert.Equal(3, res.Count);
        Assert.Equal(string.Empty, res[0].Output);
        Assert.Equal(Unit.Task, res[1].Output);
        Assert.Equal(Unit.Task, res[2].Output);
    }

    /// <summary>
    /// This unit test will execute code that has warnings, but no errors.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Should_Return_No_Errors_But_Has_Warnings()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        CodeAnalysisResult codeAnalysis = new(Enumerable.Empty<string>(), Enumerable.Empty<string>());

        // Act
        _ = await dynamicService.ExecuteCodeAsync(c =>
        {
            c.ExecuteAsConsoleApplication = true;
            c.RunCodeAnalysis = true;
            c.OutputAnalysisResult = (c) => codeAnalysis = c;
            c.AddSourceFiles($@"using System;
                            #nullable enable
                            string message = null;
                            Console.WriteLine(""Hello world!"");
                            return default;");
        });
        var expectedErrorCount = 2;

        // Assert
        Assert.NotNull(codeAnalysis.Warnings);
        Assert.NotNull(codeAnalysis.Errors);
        Assert.Equal(expectedErrorCount, codeAnalysis.Warnings.Count());
    }

    /// <summary>
    /// This unit test will throw an exception when attempting to generate code containing an invalid return type.
    /// The expected return type is an integer, while we are returning a <see cref="bool"/> value.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExecuteSampleCode_Should_Return_Errors_Code_Did_Not_Finish_Compiling_Throw_Exception()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var exception = await Record.ExceptionAsync(() => dynamicService.ExecuteCodeAsync(c =>
        {
            c.TreatWarningsAsErrors = true;
            c.AddSourceFiles($@"Console.WriteLine(""Hello world!""); return true;");
        }));

        var expectedMessage = "Source code generated with errors. See Information log for more info.";

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<DynamicCodeException>(exception);
        Assert.Equal(expectedMessage, exception.Message);
    }

    internal sealed record SampleCodeTest(string Name);
    /// <summary>
    /// This unit test will execute code and map the return data to a custom class defined.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Excute_Sample_Code_Map_To_Reference_Type()
    {

        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var res = await dynamicService.ExecuteCodeAsync<SampleCodeTest>($@"using System; public class Test {{ public object GetData() {{ return new {{ Name = ""test"" }};}} }}", (c) => { })!;

        // Assert
        Assert.Equal("test", res.Name);
    }

    /// <summary>
    /// This unit test showcases that dangerous code can still be executed and we will need to handle this in the future.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Execute_Dangerous_Blacklisted_Namespaces()
    {
        // Arrange
        var applicationStopped = false;
        var mockApplicationLifeTime = new Mock<IHostApplicationLifetime>();

        mockApplicationLifeTime.Setup(mr => mr.StopApplication())
            .Callback(() =>
            {
                applicationStopped = true;
            });
        var serviceProvider = Mock.Of<IServiceProvider>(x => x.GetService(typeof(ILoggerFactory)) == new NullLoggerFactory()
        && x.GetService(typeof(IHostApplicationLifetime)) == mockApplicationLifeTime.Object);
        ILogger<DynamicCodeService> logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        await dynamicService.ExecuteCodeAsync($@"
        using Microsoft.Extensions.Hosting;
        using System.Collections;
        using System.Reflection;
        using System.Runtime.Loader;
        using System.IO;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using System;
        using System.Linq;
        using System.Diagnostics;
        
        namespace DynamicModule.Services;
        
        
        public static class LoopHoleService
        {{
            private static string GetTheNamespaceNotAllowed() =>
                ""M,i,c,r,o,s,o,f,t.,C,o,d,e,A,n,a,l,y,s,i,s.,C,S,h,a,r,p"".Replace("","", string.Empty);
        
            private static object GetReference(string location)
            {{
                var reflection = Type.GetType(GetTheNamespaceNotAllowed() + ""MetadataReference"");
        
                return reflection.GetMethod(""CreateFromFile"").Invoke(null, new object[] {{ location }});
            }}
            public static async void BreakTheSystem(IHostApplicationLifetime lifeTime)
            {{
                lifeTime.StopApplication();
                var reflection = Type.GetType(GetTheNamespaceNotAllowed() + ""CSharpSyntaxTree"");
                
                object parsedTree = reflection
                    ?.GetMethod(""ParseText"")
                    ?.Invoke(""public static class Test {{ public static void Kill(IHostApplicationLifetime lifetime) {{ lifeTime.StopApplication(); }} }}"", null);
                if (parsedTree is not null) 
                {{
                    var someNaughtyListType = Type.GetType(GetTheNamespaceNotAllowed() + ""SyntaxTree"");
                    var listType = typeof(List<>);
                    var constructedListType = listType.MakeGenericType(someNaughtyListType);
        
                    var instance = (IList)Activator.CreateInstance(constructedListType);

                    instance.Add(parsedTree);
        
                    List<object> references = new(AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                        .Select(a => a.Location)
                        .Select(r => GetReference(r))
                        .ToArray())
                    {{
                        GetReference(typeof(object).Assembly.Location),
                        GetReference(typeof(Console).Assembly.Location),
                        GetReference(typeof(Task<>).Assembly.Location),
                        GetReference(typeof(Task).Assembly.Location),
                        GetReference(Assembly.Load(""netstandard, Version=2.0.0.0"").Location),
                        GetReference(Assembly.Load(""netstandard, Version=2.1.0.0"").Location),
                        GetReference(Assembly.GetCallingAssembly().Location),
                        GetReference(Assembly.GetExecutingAssembly().Location),
                    }};
        
                    var compilation = Type
                        .GetType(GetTheNamespaceNotAllowed() + ""CSharpCompilation"")
                    .GetMethod(""Create"")
                        .Invoke(null, new object[] {{ ""This is the assembly name"", instance, references }});
        
                    var emitResult = Type.GetType(GetTheNamespaceNotAllowed() + ""Emit.EmitResult"");
        
                    using var peStream = new MemoryStream();
                    var result = emitResult.GetMethod(""Emit"").Invoke(compilation, new object[]
                    {{
                        peStream,
                    }});
                    var assembly = new AssemblyLoadContext(""BreakTheSystem"").LoadFromStream(peStream);
        
                    var res = assembly.EntryPoint.Invoke(null, new object[] {{ lifeTime }});
        
                    if (res is Task ts)
                    {{
                        await ts;
                    }}
                }}
               
            }}
        }}
", (c) => { }, cancellationToken: CancellationToken.None);

        Assert.True(applicationStopped);
    }

    /// <summary>
    /// This unit test executes code, generates a DLL file, loads the DLL, executes the DLL, and unloads the DLL from memory.
    /// </summary>
    /// <param name="test"></param>
    /// <returns></returns>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_Console_App_Code_With_Store_Generated_File_And_Unload_The_Assembly(int test)
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        // 1 & 2, Compile, Load & Execute the DLL.
        var res = await dynamicService.ExecuteCodeAsync<int>($@"
            using System;
            public static class Test 
            {{
                public static int Init(string[] args)
                {{
                    return int.Parse(args[0]);
                }}
            }}", c =>
        {
            // 1. Save the string to a DLL.
            c.SaveGeneratedCode = true;

            // 3. Unload the DLL
            c.UnloadAssembly = true;

            c.OutputDllName = "TestUnloading";
        }, cancellationToken: CancellationToken.None, new object[] { new string[] { test.ToString() } });

        // Assert
        Assert.Equal(test, res);
        Assert.Empty(AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName!.Contains("TestUnloading.dll", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// This unit test will generate code and leave it in memory.
    /// </summary>
    /// <param name="test"></param>
    /// <returns></returns>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_Console_App_Code_With_Store_Generated_File_And_Leave_Assembly_In_Memory(int test)
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        var res = await dynamicService.ExecuteCodeAsync<int>($@"
            using System;
            namespace PappaPIsHier;
            public class Test 
            {{
                public int Init(string[] args)
                {{
                    return int.Parse(args[0]);
                }}
            }}", c =>
        {
            c.SaveGeneratedCode = true;
            c.UnloadAssembly = false;
            c.OutputDllName = "TestUnloading";
        }, cancellationToken: CancellationToken.None, new object[] { new string[] { test.ToString() } });

        // Assert
        Assert.Equal(test, res);
        Assert.NotEmpty(AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName!.Contains("TestUnloading", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Execute_Sample_Code_Use_Class_From_Different_Pre_Existing_Dll_Code()
    {
        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);

        // Act
        // 1 & 2, Compile, Load & Execute the DLL.
        var res = await dynamicService.ExecuteCodeAsync<int>($@"
            using System;
            using PappaPIsHier;
            public static class Test2
            {{
                public static int Init(Test baasVanDiePlaas)
                {{
                    return baasVanDiePlaas.Init(new string[] {{""1""}});
                }}
            }}", c =>
        {
            c.SaveGeneratedCode = true;
            c.UnloadAssembly = false;
            c.LoadExistingAssembly(Path.Combine(ExampleCodePath, "ScenarioFour", "code.dll"));
            c.OutputDllName = "TooLegitToQuit";
        }, cancellationToken: CancellationToken.None);

        var expectedOutput = 1;

        // Assert
        Assert.Equal(expectedOutput, res);
    }


    /// <summary>
    /// This unit test demonstrates a cancellation token can be used to stop the generation and execution of dynamic code,
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Execute_Sample_Code_Trigger_Cancellation_Token_Prevent_Further_Execution()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        // Arrange
        ILogger<DynamicCodeService> logger = _serviceProvider.GetService<ILoggerFactory>()!.CreateLogger<DynamicCodeService>();
        IServiceScope mockServiceScope = Mock.Of<IServiceScope>(y => y.ServiceProvider == _serviceProvider);
        IAnalyseCodeService mockAnalyseCodeService = Mock.Of<IAnalyseCodeService>(y => y.ValidateCodeAsync(It.IsAny<CSharpCompilation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()) == Task.CompletedTask);
        var mockOptions = Mock.Of<IOptions<FileExportOptions>>(x => x.Value == new FileExportOptions()
        {
            ExportPath = ExampleCodePath,
        });
        IDynamicCodeService dynamicService = new DynamicCodeService(logger, _httpContextAccesor, mockServiceScope, mockAnalyseCodeService, mockOptions);


        cancellationTokenSource.Cancel();

        // Act
        var exeption = await Record.ExceptionAsync(() => dynamicService.ExecuteCodeAsync<int>($@"
            using System;
            using TestUnloading;
            public static class Test2
            {{
                public static int Init()
                {{
                    return 1;
                }}
            }}", c =>
        {
            c.SaveGeneratedCode = true;
            c.UnloadAssembly = false;
            c.OutputDllName = "TestUnloading";
        }, cancellationToken: cancellationTokenSource.Token));

        var expectedMessage = "The operation was canceled.";

        // Assert
        Assert.NotNull(exeption);
        Assert.IsType<OperationCanceledException>(exeption);
        Assert.Equal(expectedMessage, exeption.Message);
    }
}
