namespace LogVision.Models;

public class OperationEvent
{
    public long Id { get; set; }
    public double TimestampMs { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
