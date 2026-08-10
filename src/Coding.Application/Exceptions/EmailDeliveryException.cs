namespace Coding.Exceptions;

public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
