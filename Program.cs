using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using TruckerM3U8.Hubs;
using TruckerM3U8.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RestreamService>();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddHostedService<TelemetryService>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/dashboard.html"));

app.MapGet("/version", () =>
{
    return "2.0";
});

app.Map("/mp3", async (HttpContext context, HttpResponse response) =>
{
    response.ContentType = "audio/mp3";

    //response.Headers.Connection = "close";
    response.Headers.CacheControl = "no-cache";

    using (var scope = app.Services.CreateScope())
    {
        var restreamService = scope.ServiceProvider.GetRequiredService<RestreamService>();

        restreamService.RegisterStream(response.Body);

        // keep sending stream until connection closed
        while (!context.RequestAborted.IsCancellationRequested)
        {
            await Task.Delay(100);
        }

        // cleanup
        restreamService.UnregisterStream(response.Body);
    }
});

app.MapGet("/sourceUrl", () =>
{    
    using (var scope = app.Services.CreateScope())
    {
        var restreamService = scope.ServiceProvider.GetRequiredService<RestreamService>();
        return restreamService.SourceUrl;
    }
});

app.MapPost("/sourceUrl", ([FromBody] string url) =>
{        
    using (var scope = app.Services.CreateScope())
    {
        var restreamService = scope.ServiceProvider.GetRequiredService<RestreamService>();
        restreamService.SetSourceUrl(url);
    }
});


app.MapControllers();
app.UseStaticFiles();

// Map SignalR hub
app.MapHub<TelemetryHub>("/telemetryHub");

// 啟動時開啟瀏覽器
if (app.Environment.IsProduction())
{
    Process.Start("explorer", "http://localhost:3378/dashboard.html");    
}

app.Run("http://0.0.0.0:3378");
