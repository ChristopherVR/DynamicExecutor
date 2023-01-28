namespace DynamicModule.Attributes;

/// <summary>
/// Decorate a constructor or method with this attribute to prevent it from being invoked automatically when executing dynamic code.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DontInvokeAttribute : Attribute
{

}
