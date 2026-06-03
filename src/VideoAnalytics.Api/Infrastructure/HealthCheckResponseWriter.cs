namespace VideoAnalytics.Api.Infrastructure;

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

internal static class HealthCheckResponseWriter
{
    internal static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception is not null ? "Check logs for details" : null
            }),
            duration = report.TotalDuration
        }, JsonSerializerOptions.Web);

        return context.Response.WriteAsync(result);
    }
}
