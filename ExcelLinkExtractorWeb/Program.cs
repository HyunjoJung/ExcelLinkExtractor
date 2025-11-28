using ExcelLinkExtractorWeb.Components;
using ExcelLinkExtractorWeb.Services;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient for Blazor components
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri)
});

// Register LinkExtractor service
builder.Services.AddScoped<LinkExtractorService>();

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

app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
