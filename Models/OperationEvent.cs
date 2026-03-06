using System;

namespace LogVision.Models;

public class OperationEvent
{
    public long Id { get; set; }
    public double TimestampMs { get; set; }
    public string User { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string FormattedTime => TimestampMs > 0
        ? DateTime.FromOADate(TimestampMs / 86400000.0).ToString("yyyy/MM/dd HH:mm:ss")
        : "";
}
