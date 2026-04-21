using Chassis.Gateway;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Routes.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Register the canary filter so YARP applies canary-weight metadata during config load.
builder.Services.AddSingleton<IProxyConfigFilter, CanaryRouteFilter>();

var app = builder.Build();

// Exception handler before everything else so gateway errors return clean JSON.
app.UseExceptionHandler(errorApp =>
    errorApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 502;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsync(
            """{"type":"about:blank","title":"Bad Gateway","status":502}""");
    }));

app.MapReverseProxy();

app.Run();
