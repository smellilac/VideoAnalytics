namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Tests.Fixtures;

public sealed class AddDependencyTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task AddDependency_WithValidDatasets_Returns204()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "AddDep-Source-1", "1.0.0");
        var dependsOnId = await RegisterDatasetAsync(client, "AddDep-Target-1", "1.0.0");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = dependsOnId });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_WithUnknownSourceDataset_Returns404()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var dependsOnId = await RegisterDatasetAsync(client, "AddDep-Target-2", "1.0.0");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{Guid.NewGuid()}/dependencies",
            new { DependsOnDatasetId = dependsOnId });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_WithUnknownTargetDataset_Returns404()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "AddDep-Source-3", "1.0.0");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = Guid.NewGuid() });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_SelfDependency_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "AddDep-Self-4", "1.0.0");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = datasetId });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_DuplicateDependency_Returns409()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "AddDep-Dup-Source-5", "1.0.0");
        var dependsOnId = await RegisterDatasetAsync(client, "AddDep-Dup-Target-5", "1.0.0");

        await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = dependsOnId });

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = dependsOnId });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_WithEmptyDependsOnId_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "AddDep-Empty-6", "1.0.0");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/dependencies",
            new { DependsOnDatasetId = Guid.Empty });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> RegisterDatasetAsync(HttpClient client, string name, string version)
    {
        var response = await client.PostAsJsonAsync(
            "/api/datasets",
            new RegisterDatasetRequest(name, version, $"run-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        return body!.DatasetId;
    }
}
