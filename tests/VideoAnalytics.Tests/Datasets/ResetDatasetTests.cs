namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Tests.Fixtures;

public sealed class ResetDatasetTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Reset_FailedDataset_Returns204()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "Reset-Failed", "1.0.0", "run-001");
        await TransitionToAsync(client, datasetId, DatasetStatus.InProgress);
        await TransitionToAsync(client, datasetId, DatasetStatus.Failed, "Spark OOM");

        // Act
        var response = await client.PostAsync($"/api/datasets/{datasetId}/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reset_DatasetNotFound_Returns404()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/datasets/{Guid.NewGuid()}/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_PendingDataset_Returns422()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "Reset-Pending", "1.0.0", "run-002");

        // Act — dataset is Pending, not Failed
        var response = await client.PostAsync($"/api/datasets/{datasetId}/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Reset_InProgressDataset_Returns422()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "Reset-InProgress", "1.0.0", "run-003");
        await TransitionToAsync(client, datasetId, DatasetStatus.InProgress);

        // Act
        var response = await client.PostAsync($"/api/datasets/{datasetId}/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Reset_AfterReset_DatasetIsBackToPending()
    {
        // Arrange
        using var client = fixture.CreateClient();
        var datasetId = await RegisterDatasetAsync(client, "Reset-BackToPending", "1.0.0", "run-004");
        await TransitionToAsync(client, datasetId, DatasetStatus.InProgress);
        await TransitionToAsync(client, datasetId, DatasetStatus.Failed, "Transient failure");

        // Act
        var resetResponse = await client.PostAsync($"/api/datasets/{datasetId}/reset", null);
        resetResponse.EnsureSuccessStatusCode();

        // Assert — can transition to InProgress again (Pending → InProgress allowed)
        var retryResponse = await client.PatchAsJsonAsync(
            $"/api/datasets/{datasetId}/status",
            new UpdateDatasetStatusRequest(DatasetStatus.InProgress));
        Assert.Equal(HttpStatusCode.NoContent, retryResponse.StatusCode);
    }

    private static async Task<Guid> RegisterDatasetAsync(
        HttpClient client, string name, string version, string pipelineRunId)
    {
        var request = new RegisterDatasetRequest(name, version, pipelineRunId);
        var response = await client.PostAsJsonAsync("/api/datasets", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterDatasetResponse>();
        return body!.DatasetId;
    }

    private static async Task TransitionToAsync(
        HttpClient client, Guid datasetId, DatasetStatus status, string? message = null)
    {
        var response = await client.PatchAsJsonAsync(
            $"/api/datasets/{datasetId}/status",
            new UpdateDatasetStatusRequest(status, message));
        response.EnsureSuccessStatusCode();
    }
}
