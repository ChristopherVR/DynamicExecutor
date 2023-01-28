namespace DynamicModule.Models;
/// <summary>
/// The result for the invoked <see cref="Type"/>.
/// </summary>
/// <param name="Name">The fullname for the <see cref="Type"/> that was invoked.</param>
/// <param name="Output">The data returned from the <see cref="Type"/> invoked. If this is a <see cref="void"/> or <see cref="Task"/> the return type will be a <see cref="Infrastructure.Unit"/>.</param>
public record InvocationResult<T>(string Name, T? Output);

/// <summary>
/// The result for the invoked <see cref="Type"/>.
/// </summary>
/// <param name="Name">The fullname for the <see cref="Type"/> that was invoked.</param>
/// <param name="Output">The data returned from the <see cref="Type"/> invoked. If this is a <see cref="void"/> or <see cref="Task"/> the return type will be a <see cref="Infrastructure.Unit"/>.</param>
public sealed record InvocationResult(string Name, object? Output): InvocationResult<object>(Name, Output);
