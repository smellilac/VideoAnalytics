namespace VideoAnalytics.Domain.Datasets;

public static class ArtifactFormats
{
    public const string Parquet = "parquet";
    public const string Csv = "csv";
    public const string Json = "json";

    private static readonly HashSet<string> _valid = [Parquet, Csv, Json];

    public static bool IsValid(string? format) =>
        _valid.Contains(format ?? string.Empty);

    public static string AllowedValues => string.Join(", ", _valid);
}
