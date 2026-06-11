namespace VideoAnalytics.Domain.Datasets;

public static class PipelineEventTypes
{
    public const string DatasetRegistered  = "dataset.registered";
    public const string StatusChanged      = "dataset.status_changed";
    public const string ArtifactRegistered = "dataset.artifact_registered";
    public const string DependencyAdded    = "dataset.dependency_added";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        DatasetRegistered, StatusChanged, ArtifactRegistered, DependencyAdded
    };
}
