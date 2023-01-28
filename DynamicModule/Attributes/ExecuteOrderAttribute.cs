namespace DynamicModule.Attributes;

/// <summary>
/// Decorate a method with this on a class to determine the execution order.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExecuteOrderAttribute : Attribute
{
    public int Order { get; }

    public ExecuteOrderAttribute(int order) => Order = order;
}
