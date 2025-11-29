using ExcelLinkExtractorWeb.Components;
using ExcelLinkExtractorWeb.Configuration;
using ExcelLinkExtractorWeb.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add configuration with validation
builder.Services.AddOptions<ExcelProcessingOptions>()
    .Bind(builder.Configuration.GetSection(ExcelProcessingOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxUrlLength <= 10000, "MaxUrlLength must be <= 10000.")
    .ValidateOnStart();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register LinkExtractor service with interface
builder.Services.AddScoped<ILinkExtractorService, LinkExtractorService>();
builder.Services.AddHealthChecks()
    .AddCheck<SystemHealthCheck>("system_health");

// Add rate limiting - read from configuration
var excelOptions = builder.Configuration
    .GetSection(ExcelProcessingOptions.SectionName)
    .Get<ExcelProcessingOptions>() ?? new ExcelProcessingOptions();

builder.Services.AddRateLimiter(options =>
{
    // Global rate limit: configurable requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = excelOptions.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = 429; // Too Many Requests
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Add security headers
app.Use(async (context, next) =>
{
    // Content Security Policy
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Blazor requires unsafe-eval
        "style-src 'self' 'unsafe-inline'; " + // Inline styles for Blazor
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' wss: ws:; " + // WebSocket for Blazor SignalR
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'");

    // Additional security headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

    await next();
});

// Enable rate limiting
app.UseRateLimiter();

app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();
