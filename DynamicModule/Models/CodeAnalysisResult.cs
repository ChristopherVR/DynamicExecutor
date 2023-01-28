namespace DynamicModule.Models;
public sealed record CodeAnalysisResult(IEnumerable<string> Warnings, IEnumerable<string> Errors);
