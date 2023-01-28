namespace DynamicModule.Exceptions;
public sealed class DynamicCodeException : Exception
{
    public DynamicCodeException(string message) : base(message)
    {
    }

    public DynamicCodeException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DynamicCodeException()
    {
    }
}
