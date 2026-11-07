using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Common;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddFreelanceFlowProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                if (ctx.Exception is DomainException de)
                {
                    ctx.ProblemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                    ctx.ProblemDetails.Type = $"https://freelanceflow/errors/{de.Code}";
                    ctx.ProblemDetails.Extensions["code"] = de.Code;
                    ctx.HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                }
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            };
        });
        return services;
    }
}
