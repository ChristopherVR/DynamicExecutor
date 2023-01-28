using System.Reflection;

namespace DynamicModule.Extensions;

internal static class AppDomainExtensions
{
    /// <summary>
    /// Returns a list of <see cref="Assembly"/> references used by the <see cref="AppDomain"/>.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    internal static List<string> GetAllReferences(this AppDomain domain)
    {
        var domainAssemblies = domain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.FullName))
            .Select(a => a.FullName);

        var currentAssemblies = domain.GetAssemblies()
            ?.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.FullName))
            ?.Select(a => a.FullName!) ?? Enumerable.Empty<string>();

        var entryAssemby = Assembly
            .GetEntryAssembly();

        var entryAssemblies = entryAssemby
            ?.GetReferencedAssemblies()
            ?.Select(Assembly.Load)
            ?.Where(z => !string.IsNullOrWhiteSpace(z?.FullName))
            ?.Select(y => y.Location)
            ?? Enumerable.Empty<string>();

        if (entryAssemby?.FullName is not null)
        {
            entryAssemblies = entryAssemblies.Append(entryAssemby.FullName);
        }

        var allReferences = new List<string?>(currentAssemblies.Concat(entryAssemblies))
        {
            typeof(object).Assembly.FullName!,
            typeof(Console).Assembly.FullName!,
            typeof(Task<>).Assembly.FullName!,
            typeof(Task).Assembly.Location!,
            Assembly.Load("netstandard, Version=2.0.0.0").FullName!,
            Assembly.Load("netstandard, Version=2.1.0.0").FullName!,
            Assembly.GetCallingAssembly().FullName,
            Assembly.GetExecutingAssembly().FullName,
        }
        .Distinct()
        .Where(z => !string.IsNullOrWhiteSpace(z))
        .Cast<string>()
        .ToList();

        return allReferences;
    }

    /// <summary>
    /// Returns a list of <see cref="Assembly"/> references used by the <see cref="AppDomain"/>.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    internal static List<string> GetCurrentDomainReferences() => AppDomain.CurrentDomain.GetAllReferences();
}
