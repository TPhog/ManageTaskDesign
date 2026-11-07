using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Domain.Common;
using Serilog;
using Serilog.Formatting.Compact;

// Builder setup
var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new CompactJsonFormatter()));

// Services
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddFreelanceFlowProblemDetails();
// DbContext registration will be added in Task 3
// Auth registration will be added in Task 7

// App
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Health check endpoint
app.MapGet("/health", () =>
{
    // Simple health check - returns 200 OK
    // DB ping will be added in Task 3
    return Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
});

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
