using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nudge.Api.Infrastructure;

// One place that maps our exceptions to status codes and a standard JSON error body
// ("problem details": { "status": 404, "detail": "User not found." }).
public class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException appException)
            return false;   // anything else is a real bug: let it become a 500

        context.Response.StatusCode = appException.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = ReasonPhrase(appException.StatusCode),
                Detail = appException.Message
            }
        });
    }

    private static string ReasonPhrase(int status) =>
        Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status);
}
