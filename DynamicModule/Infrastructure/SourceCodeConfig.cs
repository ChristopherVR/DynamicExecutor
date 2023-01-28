using DynamicModule.Exceptions;
using DynamicModule.Infrastructure.Interfaces;
using DynamicModule.Models;
using Microsoft.CodeAnalysis;
using System.ComponentModel.DataAnnotations;

namespace DynamicModule.Infrastructure;
public record SourceCodeConfig : ISourceCodeConfig
{
    /// <inheritdoc />
    public bool UnloadAssembly { get; set; }

    /// <inheritdoc />
    public bool RunCodeAnalysis { get; set; }

    /// <inheritdoc />
    public bool StoreCodeAnalysis { get; set; }

    /// <inheritdoc />
    public bool SaveGeneratedCode { get; set; }

    /// <inheritdoc />
    public string OutputDllName { get; set; } = null!;

    /// <inheritdoc />
    public bool TreatWarningsAsErrors { get; set; }

    /// <inheritdoc />
    public bool ExecuteAsConsoleApplication { get; set; }

    /// <inheritdoc />
    public bool IgnoreErrors { get; set; }

    /// <inheritdoc />
    public bool AllowUnsafe { get; set; }

    /// <inheritdoc />
    public NullableContextOptions NullableContextOptions { get; set; } = NullableContextOptions.Disable;

    /// <inheritdoc />
    public Action<CodeAnalysisResult>? OutputAnalysisResult { get; set; }

    /// <inheritdoc />
    public ICollection<(HttpClientHandler ClientHandler, ICollection<string> Assemblies)> CustomAssemblies { get; private set; }
        = Array.Empty<(HttpClientHandler ClientHandler, ICollection<string> Assemblies)>();

    /// <inheritdoc />
    public OptimizationLevel OptimizeLevel { get; private set; } = OptimizationLevel.Release;

    /// <inheritdoc />
    public IList<string> ExistingAssembliesToLoad { get; private set; } = new List<string>();

    internal object[]? Arguments { get; private set; }
    /// <inheritdoc />
    object[]? ISourceCodeConfig.Arguments => Arguments;

    /// <inheritdoc />
    internal bool LoadExistingAssemblies { get; set; }
    bool ISourceCodeConfig.LoadExistingAssemblies => LoadExistingAssemblies;

    /// <inheritdoc />
    public void AddCustomAssemblies(ICollection<string> customAssemblyUrls, HttpClientHandler httpClientHandler)
    {
        foreach (string customAssemblyUrl in customAssemblyUrls)
        {
            if (!new UrlAttribute().IsValid(customAssemblyUrl))
            {
                throw new DynamicCodeException("Custom assembly is not a valid URL.");
            }
        }

        CustomAssemblies.Add((httpClientHandler, customAssemblyUrls));
    }

    internal void SetArguments(object[] arguments) => Arguments = arguments;

    /// <inheritdoc />
    public void SetOptimizeToReleaseMode() => OptimizeLevel = OptimizationLevel.Release;

    /// <inheritdoc />
    public void SetOptimizeToDebugMode() => OptimizeLevel = OptimizationLevel.Debug;

    public void LoadExistingAssembly(string fileName)
    {
        LoadExistingAssemblies = true;
        ExistingAssembliesToLoad.Add(fileName);
    }
}
