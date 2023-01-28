using Microsoft.CodeAnalysis.CSharp;

namespace DynamicModule.Services.Interfaces;
public interface IAnalyseCodeService
{
    /// <summary>
    /// Validates the dynamic code against certain semantics, blacklisted namespaces and any other configuration required.
    /// </summary>
    /// <param name="cSharpCompilation">Generated dynamic code that will be used in the analysis.</param>
    /// <param name="storeAnalysis">Indicates if the analysis should be stored onto the CDN.</param>
    /// <returns>A <see cref="Task"/> indicating the success status.</returns>
    Task ValidateCodeAsync(CSharpCompilation cSharpCompilation, bool storeAnalysis, CancellationToken cancellationToken = default);
}
