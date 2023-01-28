using DynamicModule.Models;
using Microsoft.CodeAnalysis;

namespace DynamicModule.Infrastructure.Interfaces;
public interface ISourceCodeConfig
{
    /// <summary>
    /// Forces the garbage collection to run and remove any dynamic assemblies currently in memory.
    /// </summary>
    bool UnloadAssembly { get; }

    /// <summary>
    /// Analyse the code after it has been generated.
    /// </summary>
    bool RunCodeAnalysis { get; }

    /// <summary>
    /// Stores the analysed code. Requires <see cref="RunCodeAnalysis"/> to be set to <see cref="true"/>.
    /// </summary>
    bool StoreCodeAnalysis { get; }

    /// <summary>
    /// Saves the generated code.
    /// </summary>
    bool SaveGeneratedCode { get; }

    /// <summary>
    /// Sets the in memory <see cref="System.Reflection.Assembly"/> name.
    /// </summary>
    string OutputDllName { get; }

    /// <summary>
    /// Will treat any compilation warnings as errors.
    /// </summary>
    bool TreatWarningsAsErrors { get; }

    /// <summary>
    /// Ignores any errors related to the execution of the dynamic code.
    /// Returns a null value in references where the invocation failed.
    /// </summary>
    bool IgnoreErrors { get; }

    /// <summary>
    /// Executes the code as a console application. This is required when running top-level statement logic.
    /// <br/>Note that if the code is executed as a console application, the generated file will be stored as an executable.
    /// </summary>
    bool ExecuteAsConsoleApplication { get; }

    /// <summary>
    /// Determines if the <see cref="unsafe"/> modifier is allowed in the dynamic code.
    /// </summary>
    bool AllowUnsafe { get; }

    /// <summary>
    /// Represents the default state of nullable analysis in this compilation.
    /// </summary>
    NullableContextOptions NullableContextOptions { get; }

    /// <summary>
    /// A list of dynamic assemblies stored on the server to load. The assemblies will use <see cref="Options.FileExportOptions.ExportPath"/> to determine the assemblies' location.
    /// </summary>
    IList<string> ExistingAssembliesToLoad { get; }

    /// <summary>
    /// Loads existing assemblies from the default Output path. This is useful in cases where dynamic code depends on pre-existing generated dynamic code.
    /// </summary>
    internal bool LoadExistingAssemblies { get; }

    /// <summary>
    /// Function to output the code analysis result to if <see cref="RunCodeAnalysis"/> is enabled.
    /// </summary>
    Action<CodeAnalysisResult>? OutputAnalysisResult { get; }

    ICollection<(HttpClientHandler ClientHandler, ICollection<string> Assemblies)> CustomAssemblies { get; }

    OptimizationLevel OptimizeLevel { get; }

    internal object[]? Arguments { get; }

    /// <summary>
    /// Add a list of custom assembly Urls.
    /// </summary>
    /// <param name="customAssemblyUrls"></param>
    /// <param name="httpClientHandler">Client handler to download the custom assemblies from.</param>
    void AddCustomAssemblies(ICollection<string> customAssemblyUrls, HttpClientHandler httpClientHandler);

    /// <summary>
    /// Sets the optimization mode for the compiled code to release mode.
    /// </summary>
    void SetOptimizeToReleaseMode();

    /// <summary>
    /// Sets the optimization mode for the compiled code to debug mode.
    /// </summary>
    void SetOptimizeToDebugMode();
}
