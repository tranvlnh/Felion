using Felion.Api;
using Felion.Application.Bootstrap;
using Felion.Bot;
using Felion.Host.Authentication;
using Felion.Host.Middleware;
using Felion.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var isBootstrapAdminCommand = args is [var command]
    && string.Equals(command, "bootstrap-admin", StringComparison.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(isBootstrapAdminCommand ? [] : args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
    });
}
else
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "O";
    });
}

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId)
            && correlationId is not null)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});

builder.Services.AddOpenApi();
builder.Services.AddFelionWebAuthentication(builder.Configuration);

var healthChecks = builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

builder.Services.AddFelionInfrastructure(builder.Configuration, healthChecks);
builder.Services.AddFelionBot(builder.Configuration, healthChecks);

var app = builder.Build();

if (isBootstrapAdminCommand)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var bootstrapService = scope.ServiceProvider.GetRequiredService<BootstrapService>();
        var bootstrapSection = builder.Configuration.GetSection("Bootstrap");
        var bootstrapCommand = new BootstrapAdminCommand(
            bootstrapSection["StudentId"] ?? string.Empty,
            bootstrapSection["FullName"] ?? string.Empty,
            bootstrapSection["ClubEmail"] ?? string.Empty,
            bootstrapSection["GenerationName"] ?? string.Empty,
            bootstrapSection["GenerationCode"] ?? string.Empty);
        var result = await bootstrapService.BootstrapAdminAsync(bootstrapCommand);
        Console.WriteLine($"Bootstrap completed. Admin={result.ClubEmail}; MemberId={result.MemberId}; Generation={result.GenerationName} ({result.GenerationId})");
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"[Bootstrap Failed] {exception.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Discord:Token"]))
{
    app.AddFelionBotModules();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Environment.IsEnvironment("Testing"))
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

app.UseAuthentication();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<TemporaryActorMiddleware>();
}

app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live", StringComparer.Ordinal)
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready", StringComparer.Ordinal)
});

app.MapFelionApi();

app.Run();

public partial class Program;
