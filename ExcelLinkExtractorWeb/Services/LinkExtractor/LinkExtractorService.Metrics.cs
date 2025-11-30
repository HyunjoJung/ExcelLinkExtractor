using Prometheus;

namespace ExcelLinkExtractorWeb.Services.LinkExtractor;

public partial class LinkExtractorService
{
    private static readonly Counter ProcessCounter = global::Prometheus.Metrics.CreateCounter(
        "sheetlink_files_total",
        "Total files processed by operation and status.",
        new CounterConfiguration { LabelNames = new[] { "operation", "status" } });

    private static readonly Histogram ProcessDuration = global::Prometheus.Metrics.CreateHistogram(
        "sheetlink_process_duration_seconds",
        "Processing duration per operation.",
        new HistogramConfiguration { LabelNames = new[] { "operation" } });

    private static readonly Counter RowsProcessed = global::Prometheus.Metrics.CreateCounter(
        "sheetlink_rows_total",
        "Total rows processed.",
        new CounterConfiguration { LabelNames = new[] { "operation" } });

    private static readonly Counter InputBytes = global::Prometheus.Metrics.CreateCounter(
        "sheetlink_input_bytes_total",
        "Total input bytes processed.",
        new CounterConfiguration { LabelNames = new[] { "operation" } });

    private static void RecordPrometheusMetrics(string operation, string status, ProcessContext context, TimeSpan duration)
    {
        ProcessCounter.WithLabels(operation, status).Inc();
        ProcessDuration.WithLabels(operation).Observe(duration.TotalSeconds);
        RowsProcessed.WithLabels(operation).Inc(context.Rows);
        InputBytes.WithLabels(operation).Inc(context.InputBytes);
    }
}
