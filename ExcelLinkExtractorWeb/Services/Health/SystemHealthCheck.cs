using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExcelLinkExtractorWeb.Services.Health;

/// <summary>
/// Basic system health check to ensure the process has headroom (memory/disk).
/// </summary>
public class SystemHealthCheck : IHealthCheck
{
    private const long MinimumFreeBytes = 200 * 1024 * 1024; // 200MB
    private const long MaximumAllocatedBytes = 1L * 1024 * 1024 * 1024; // 1GB

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        var drive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.IsReady && d.RootDirectory.FullName == Path.GetPathRoot(AppContext.BaseDirectory))
            ?? DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);

        if (allocated > MaximumAllocatedBytes)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Memory usage high: {allocated / (1024 * 1024)}MB"));
        }

        if (drive != null && drive.AvailableFreeSpace < MinimumFreeBytes)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Low disk space: {drive.AvailableFreeSpace / (1024 * 1024)}MB free"));
        }

        var data = new Dictionary<string, object>
        {
            ["allocatedBytes"] = allocated,
            ["drive"] = drive?.Name ?? "unknown",
            ["freeBytes"] = drive?.AvailableFreeSpace ?? -1L
        };

        return Task.FromResult(HealthCheckResult.Healthy("OK", data));
    }
}
