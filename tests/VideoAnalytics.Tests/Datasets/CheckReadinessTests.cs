namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.CheckReadiness;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Tests.Fixtures;

public sealed class CheckReadinessTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CheckReadiness_DatasetNotFound_Returns404()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/datasets/{Guid.NewGuid()}/readiness");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckReadiness_NoDependencies_ReturnsReady()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "Readiness-NoDeps", "1.0.0");

        // Act
        var response = await client.GetAsync($"/api/datasets/{datasetId}/readiness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckReadinessResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsReady);
        Assert.Null(body.Reason);
    }

    [Fact]
    public async Task CheckReadiness_WithPendingDependency_ReturnsNotReady()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var depId = await RegisterDatasetAsync(client, "Readiness-Dep-Pending", "1.0.0");
        var datasetId = await RegisterDatasetAsync(client, "Readiness-HasPendingDep", "1.0.0");

        await AddDependencyAsync(client, datasetId, depId);

        // Act
        var response = await client.GetAsync($"/api/datasets/{datasetId}/readiness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckReadinessResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsReady);
        Assert.NotNull(body.Reason);
        Assert.Contains("not ready", body.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckReadiness_TransitiveDependencyNotReady_ReturnsNotReady()
    {
        // Arrange: A → B → C (C is Pending)
        using var client = fixture.CreateClient();
        var cId = await RegisterDatasetAsync(client, "Readiness-Transitive-C", "1.0.0");
        var bId = await RegisterDatasetAsync(client, "Readiness-Transitive-B", "1.0.0");
        var aId = await RegisterDatasetAsync(client, "Readiness-Transitive-A", "1.0.0");

        await AddDependencyAsync(client, aId, bId);
        await AddDependencyAsync(client, bId, cId);

        // Act — A's transitive dep C is not ready
        var response = await client.GetAsync($"/api/datasets/{aId}/readiness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckReadinessResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsReady);
    }

    private static async Task<Guid> RegisterDatasetAsync(HttpClient client, string name, string version)
    {
        var request = new RegisterDatasetRequest(name, version, $"run-readiness-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/datasets", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        return body!.DatasetId;
    }

    private static async Task AddDependencyAsync(HttpClient client, Guid datasetId, Guid dependsOnDatasetId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = dependsOnDatasetId });
        response.EnsureSuccessStatusCode();
    }
}
