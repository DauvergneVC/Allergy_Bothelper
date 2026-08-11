/// <summary>
/// OCR-5: typed failure raised inside the OCR path (Vision gRPC errors, network or
/// authentication failures, rejected photos). Handlers catch this to reply with a
/// friendly ES/EN message instead of crashing the webhook.
/// </summary>
public sealed class OcrFailureException : Exception
{
    public OcrFailureException(string message) : base(message)
    {
    }

    public OcrFailureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
