namespace VideoAnalytics.Infrastructure.Persistence;

using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Application.Reporting.GetEngagementReport;

public sealed class ClickHouseReportRepository(IOptions<ClickHouseSettings> options) : IReportRepository
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<IReadOnlyList<EngagementMetricDto>> GetEngagementMetricsAsync(
        string platform,
        DateOnly dateFrom,
        DateOnly dateTo,
        int limit,
        CancellationToken cancellationToken)
    {
        // ClickHouse parameterized syntax: {name:Type} in SQL; DbParameter per parameter
        const string sql = """
            SELECT
                video_id,
                platform,
                recorded_at,
                views,
                likes,
                comments,
                shares,
                engagement_rate,
                category,
                tags
            FROM video_engagement_metrics
            WHERE platform = {platform:String}
              AND recorded_at >= {dateFrom:DateTime}
              AND recorded_at <  {dateTo:DateTime}
            ORDER BY engagement_rate DESC, recorded_at DESC
            LIMIT {limit:Int32}
            """;

        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        AddParameter(command, "platform", platform);
        AddParameter(command, "dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        AddParameter(command, "dateTo", dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue));
        AddParameter(command, "limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<EngagementMetricDto>();
        
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EngagementMetricDto(
                VideoId: reader.GetString(0),
                Platform: reader.GetString(1),
                RecordedAt: new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
                Views: reader.GetInt64(3),
                Likes: reader.GetInt64(4),
                Comments: reader.GetInt64(5),
                Shares: reader.GetInt64(6),
                EngagementRate: reader.GetDouble(7),
                Category: reader.GetString(8),
                Tags: reader.GetValue(9) is string[] tags ? tags : []));
        }

        return results;
    }

    private static void AddParameter(ClickHouseCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
