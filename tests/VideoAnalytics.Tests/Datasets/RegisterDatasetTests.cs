namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Tests.Fixtures;

public sealed class RegisterDatasetTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task RegisterDataset_WithValidRequest_Returns201WithDatasetId()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("Test Dataset", "1.0.0", "run-abc-123");

        // Act
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.DatasetId);
        Assert.Equal("Test Dataset", body.Name);
        Assert.Equal("1.0.0", body.Version);
        Assert.Equal("Pending", body.Status);
        Assert.True(body.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task RegisterDataset_WithLocationHeader_PointsToNewDataset()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("Location Header Test", "2.0.0", "run-def-456");

        // Act
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        Assert.NotNull(body);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(body.DatasetId.ToString(), response.Headers.Location.ToString());
    }

    [Fact]
    public async Task RegisterDataset_WithEmptyName_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("", "1.0.0", "run-abc-123");

        // Act
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDataset_WithEmptyVersion_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("Valid Name", "", "run-abc-123");

        // Act
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDataset_WithEmptyPipelineRunId_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("Valid Name", "1.0.0", "");

        // Act
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDataset_DuplicateNameAndVersion_Returns409()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterDatasetRequest("Duplicate Dataset", "1.0.0", "run-dup-001");

        // Act
        await client.PostAsJsonAsync("/api/datasets", request);
        var response = await client.PostAsJsonAsync("/api/datasets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
