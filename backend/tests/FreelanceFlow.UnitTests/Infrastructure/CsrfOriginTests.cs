using FluentAssertions;
using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.UnitTests.Infrastructure;

public class CsrfOriginTests
{
    private static async Task<int> Post(string? origin, string[] additionalOrigins)
    {
        var settings = Options.Create(new AppSettings { PublicBaseUrl = "http://localhost:5173", AdditionalOrigins = additionalOrigins });
        var reached = false;
        var middleware = new CsrfMiddleware(_ => { reached = true; return Task.CompletedTask; }, settings);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Put;
        ctx.Request.Path = "/api/admin/settings";
        if (origin is not null) ctx.Request.Headers.Origin = origin;
        ctx.Request.Headers["X-CSRF-Token"] = "tok";
        ctx.Request.Headers.Cookie = "ff_csrf=tok";

        await middleware.InvokeAsync(ctx);
        return reached ? StatusCodes.Status200OK : ctx.Response.StatusCode;
    }

    [Fact]
    public async Task Public_base_url_origin_passes()
        => (await Post("http://localhost:5173", [])).Should().Be(200);

    [Fact]
    public async Task Configured_additional_origin_passes()
        => (await Post("https://cam.akers.date/", ["https://cam.akers.date"])).Should().Be(200);

    [Fact]
    public async Task Unknown_origin_is_forbidden()
        => (await Post("https://evil.example", ["https://cam.akers.date"])).Should().Be(403);
}
