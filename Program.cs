using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TruckerM3U8.Hubs;
using TruckerM3U8.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RestreamService>();
builder.Services.AddSignalR();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddHostedService<TelemetryService>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/dashboard.html"));

app.MapGet("/version", () =>
{
    return "2.1";
});

app.MapGet("/ip", () =>
{
    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
        {
            return ip.ToString();
        }
    }
    return "";
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


// Trimmed Code (AOT) will not include full object
// System.NotSupportedException: JsonTypeInfo metadata for type 'System.String' was not provided by TypeInfoResolver of type '[]'. 
[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
[System.Text.Json.Serialization.JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, int>))]
internal partial class AppJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
