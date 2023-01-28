using Microsoft.AspNetCore.Http;

namespace DynamicModule.Infrastructure.Interfaces;
public interface IDynamicCodeConfig : ISourceCodeConfig
{
    /// <summary>
    /// A folder location on the local system that contains a list of source files to compile and the list of previous compiled dynamic code.
    /// </summary>
    string? FolderPath { get; }

    /// <summary>
    /// List of <see cref="IFormFile"/> files to generate into compiled code.
    /// </summary>
    IEnumerable<IFormFile> Files { get; }

    /// <summary>
    /// List of <see cref="string"/> source files to generate into compiled code.
    /// </summary>
    IEnumerable<string> SourceFiles { get; }
}
