using DynamicModule.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DynamicModule.Infrastructure;
public sealed record DynamicCodeConfig : SourceCodeConfig, IDynamicCodeConfig
{
    /// <inheritdoc />
    public string? FolderPath { get; set; }
    /// <inheritdoc />
    public IEnumerable<IFormFile> Files { get; private set; } = Array.Empty<IFormFile>();

    /// <inheritdoc />
    public IEnumerable<string> SourceFiles { get; private set; } = Array.Empty<string>();

    /// <inheritdoc />
    public void AddSourceFiles(params string[] sourceFiles) => SourceFiles = SourceFiles.Concat(sourceFiles);
}
