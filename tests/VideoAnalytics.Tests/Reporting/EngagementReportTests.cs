namespace VideoAnalytics.Tests.Reporting;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Reporting.GetEngagementReport;
using VideoAnalytics.Tests.Fixtures;

public sealed class EngagementReportTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task GetEngagementReport_WithNoUnreadyDatasets_Returns200WithMetrics()
    {
        // Arrange — no datasets registered for "engagement_metrics" in this date range,
        // so CheckRangeReadinessAsync returns 0 issues and the guard passes.
        // NullReportRepository returns an empty metrics list.
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/api/reports/engagement?platform=tiktok&dateFrom=2020-01-01&dateTo=2020-01-07&limit=50");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetEngagementReportResponse>();
        Assert.NotNull(body);
        Assert.Equal("tiktok", body.Platform);
        Assert.Equal(new DateOnly(2020, 1, 1), body.DateFrom);
        Assert.Equal(new DateOnly(2020, 1, 7), body.DateTo);
        Assert.Empty(body.Metrics);
        Assert.Equal(0, body.Total);
    }

    [Fact]
    public async Task GetEngagementReport_WhenDatasetIsNotReady_Returns503WithIssues()
    {
        // Arrange — register an "engagement_metrics" dataset in Pending status for the target date
        using var client = fixture.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/datasets", new RegisterDatasetRequest(
            Name: "engagement_metrics",
            Version: "2024-06-01",
            PipelineRunId: "test-run-reporting-001"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // Act — request covers the date with the pending dataset
        var response = await client.GetAsync(
            "/api/reports/engagement?platform=instagram&dateFrom=2024-06-01&dateTo=2024-06-01&limit=100");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(body);
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Data not ready for requested period", body.RootElement.GetProperty("title").GetString());

        var issues = body.RootElement.GetProperty("issues").EnumerateArray().ToList();
        Assert.Single(issues);
        Assert.Equal("2024-06-01", issues[0].GetProperty("date").GetString());
        Assert.Contains("Pending", issues[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetEngagementReport_WithInvalidPlatform_Returns400()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(
            "/api/reports/engagement?platform=&dateFrom=2024-01-01&dateTo=2024-01-07&limit=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEngagementReport_WhenDateFromAfterDateTo_Returns400()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(
            "/api/reports/engagement?platform=tiktok&dateFrom=2024-01-10&dateTo=2024-01-01&limit=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEngagementReport_WithLimitOutOfRange_Returns400()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(
            "/api/reports/engagement?platform=tiktok&dateFrom=2024-01-01&dateTo=2024-01-07&limit=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
