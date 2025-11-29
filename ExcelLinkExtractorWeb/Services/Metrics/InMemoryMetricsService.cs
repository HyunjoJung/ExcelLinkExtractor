namespace ExcelLinkExtractorWeb.Services.Metrics;

/// <summary>
/// Simple in-memory metrics collector for file processing and errors.
/// </summary>
public interface IMetricsService
{
    void RecordFileProcessed(long fileSizeBytes, int rowCount, TimeSpan duration);
    void RecordError(string errorType);
    MetricsSnapshot GetSnapshot();
}

public sealed class InMemoryMetricsService : IMetricsService
{
    private long _filesProcessed;
    private long _totalRows;
    private long _totalBytes;
    private long _totalDurationMs;
    private readonly Dictionary<string, long> _errors = new();
    private readonly object _lock = new();

    public void RecordFileProcessed(long fileSizeBytes, int rowCount, TimeSpan duration)
    {
        lock (_lock)
        {
            _filesProcessed++;
            _totalRows += rowCount;
            _totalBytes += fileSizeBytes;
            _totalDurationMs += (long)duration.TotalMilliseconds;
        }
    }

    public void RecordError(string errorType)
    {
        lock (_lock)
        {
            if (!_errors.ContainsKey(errorType))
            {
                _errors[errorType] = 0;
            }
            _errors[errorType]++;
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new MetricsSnapshot
            {
                FilesProcessed = _filesProcessed,
                TotalRows = _totalRows,
                TotalBytes = _totalBytes,
                TotalDurationMs = _totalDurationMs,
                Errors = new Dictionary<string, long>(_errors)
            };
        }
    }
}

public sealed class MetricsSnapshot
{
    public long FilesProcessed { get; set; }
    public long TotalRows { get; set; }
    public long TotalBytes { get; set; }
    public long TotalDurationMs { get; set; }
    public Dictionary<string, long> Errors { get; set; } = new();
}
