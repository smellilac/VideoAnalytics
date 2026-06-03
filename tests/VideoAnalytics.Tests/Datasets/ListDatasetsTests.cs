namespace VideoAnalytics.Tests.Datasets;

using System.Net;
using System.Net.Http.Json;
using VideoAnalytics.Api.Endpoints.DatasetLifecycle;
using VideoAnalytics.Application.Datasets.ListDatasets;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Tests.Fixtures;

public sealed class ListDatasetsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ListDatasets_WithNoDatasets_ReturnsEmptyList()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/datasets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
        Assert.Equal(0, body.Total);
    }

    [Fact]
    public async Task ListDatasets_WithRegisteredDatasets_ReturnsItems()
    {
        // Arrange
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("List Test A", "1.0.0", "run-list-a"));
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("List Test B", "1.0.0", "run-list-b"));

        // Act
        var response = await client.GetAsync("/api/datasets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        Assert.True(body.Items.Count >= 2);
        Assert.True(body.Total >= 2);
    }

    [Fact]
    public async Task ListDatasets_ItemsOrderedByCreatedAtDescending()
    {
        // Arrange
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("Order Test First", "1.0.0", "run-order-1"));
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("Order Test Second", "1.0.0", "run-order-2"));

        // Act
        var response = await client.GetAsync("/api/datasets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        var items = body.Items.ToList();
        for (var i = 1; i < items.Count; i++)
            Assert.True(items[i - 1].CreatedAt >= items[i].CreatedAt);
    }

    [Fact]
    public async Task ListDatasets_ResponseContainsExpectedFields()
    {
        // Arrange
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("Fields Test", "1.0.0", "run-fields-001"));

        // Act
        var response = await client.GetAsync("/api/datasets");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        var item = body.Items.FirstOrDefault(x => x.Name == "Fields Test" && x.Version == "1.0.0");
        Assert.NotNull(item);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("run-fields-001", item.PipelineRunId);
        Assert.Equal("Pending", item.Status);
        Assert.True(item.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ListDatasets_PaginationWithSkipAndTake_ReturnsCorrectSlice()
    {
        // Arrange
        using var client = fixture.CreateClient();
        for (var i = 1; i <= 3; i++)
            await client.PostAsJsonAsync("/api/datasets",
                new RegisterDatasetRequest($"Pagination Dataset", $"{i}.0.0", $"run-page-{i}"));

        var totalResponse = await client.GetAsync("/api/datasets?skip=0&take=100");
        var totalBody = await totalResponse.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(totalBody);
        var total = totalBody.Total;

        // Act
        var response = await client.GetAsync("/api/datasets?skip=0&take=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(total, body.Total);
    }

    [Fact]
    public async Task ListDatasets_WithStatusFilter_ReturnsOnlyMatchingDatasets()
    {
        // Arrange
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/datasets",
            new RegisterDatasetRequest("Status Filter Test", "1.0.0", "run-status-filter-001"));

        // Act
        var response = await client.GetAsync("/api/datasets?status=Pending");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListDatasetsResponse>();
        Assert.NotNull(body);
        Assert.All(body.Items, item => Assert.Equal("Pending", item.Status));
    }

    [Fact]
    public async Task ListDatasets_WithInvalidTake_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/datasets?take=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListDatasets_WithTakeExceedingMaximum_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/datasets?take=101");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListDatasets_WithNegativeSkip_Returns400()
    {
        // Arrange
        using var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/datasets?skip=-1");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
