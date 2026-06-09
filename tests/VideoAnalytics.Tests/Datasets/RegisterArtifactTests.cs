namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.RegisterArtifact;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Tests.Fixtures;

public sealed class RegisterArtifactTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task RegisterArtifact_WithValidRequest_Returns201WithArtifactId()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-Basic");
        var request = new RegisterArtifactRequest("datasets/v1/part-0000.parquet", "parquet", 1_024_000, 5_000);

        // Act
        var response = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ArtifactId);
        Assert.Equal(datasetId, body.DatasetId);
        Assert.Equal("datasets/v1/part-0000.parquet", body.S3Key);
        Assert.Equal("parquet", body.ArtifactType);
        Assert.Equal(1_024_000, body.SizeBytes);
        Assert.Equal(5_000, body.RowCount);
        Assert.True(body.RegisteredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task RegisterArtifact_SameS3Key_IsIdempotent()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-Idempotent");
        var request = new RegisterArtifactRequest("datasets/idempotent/part-0000.parquet", "parquet", 512_000, 2_500);

        // Act — register twice
        var firstResponse = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);
        var secondResponse = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);

        // Assert — both succeed; second returns the same artifact
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody.ArtifactId, secondBody.ArtifactId);
    }

    [Fact]
    public async Task RegisterArtifact_DifferentS3Keys_RegistersMultipleArtifacts()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-MultiPart");

        // Act
        var r1 = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts",
            new RegisterArtifactRequest("datasets/multi/part-0000.parquet", "parquet", 512_000, 2_500));
        var r2 = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts",
            new RegisterArtifactRequest("datasets/multi/part-0001.parquet", "parquet", 490_000, 2_400));

        // Assert
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var body1 = await r1.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        var body2 = await r2.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        Assert.NotNull(body1);
        Assert.NotNull(body2);
        Assert.NotEqual(body1.ArtifactId, body2.ArtifactId);
    }

    [Fact]
    public async Task RegisterArtifact_DatasetNotFound_Returns404()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var request = new RegisterArtifactRequest("datasets/missing/part-0000.parquet", "parquet", 1_000, 100);

        // Act
        var response = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/artifacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegisterArtifact_WithEmptyS3Key_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-Validation");
        var request = new RegisterArtifactRequest("", "parquet", 1_000, 100);

        // Act
        var response = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterArtifact_WithZeroSizeBytes_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-ZeroSize");
        var request = new RegisterArtifactRequest("datasets/zero/part-0000.parquet", "parquet", 0, 100);

        // Act
        var response = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterArtifact_LocationHeader_PointsToArtifact()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "ArtifactTest-Location");
        var request = new RegisterArtifactRequest("datasets/location/part-0000.parquet", "parquet", 100, 10);

        // Act
        var response = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/artifacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterArtifactResponse>();
        Assert.NotNull(body);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(body.ArtifactId.ToString(), response.Headers.Location.ToString());
    }

    private static async Task<Guid> RegisterDatasetAsync(HttpClient client, string name)
    {
        var request = new RegisterDatasetRequest(name, "1.0.0", "run-artifact-001");
        var response = await client.PostAsJsonAsync("/api/datasets", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        return body!.DatasetId;
    }
}
