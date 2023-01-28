using Microsoft.Extensions.Logging;

namespace DynamicModule.Extensions;
internal static class LoggerExtensions
{
    /// <summary>
    /// Creates a generic <see cref="Logger{T}"/> logger using reflection.
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="declaredType"></param>
    /// <returns></returns>
    internal static object CreateGenericLogger(this ILoggerFactory factory, Type declaredType)
    {
        Type genericClass = typeof(ILogger<>).MakeGenericType(declaredType);
        const string CreateLogger = nameof(CreateLogger);
        var genericType = declaredType.GetGenericArguments().First();
        var mi = typeof(LoggerFactoryExtensions).GetMethods().Single(m => m.Name == CreateLogger && m.IsGenericMethodDefinition);
        var gi = mi.MakeGenericMethod(declaredType.GetGenericArguments().First());
        return gi.Invoke(null, new[] { factory })!;
    }
}
