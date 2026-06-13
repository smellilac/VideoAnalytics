namespace VideoAnalytics.Application.Datasets.Common;

using System.Net;
using ErrorOr;
using VideoAnalytics.Domain.Datasets;

public static class DatasetErrors
{
    private const string Prefix = "Dataset";

    public static class Codes
    {
        public const string AlreadyExists     = $"{Prefix}.AlreadyExists";
        public const string NotFound          = $"{Prefix}.NotFound";
        public const string InvalidTransition = $"{Prefix}.InvalidTransition";
        public const string DependenciesNotReady = $"{Prefix}.DependenciesNotReady";
        public const string ArtifactMissing   = $"{Prefix}.ArtifactMissing";
        public const string InvalidStatus     = $"{Prefix}.InvalidStatus";
        public const string DependencyTargetNotFound = $"{Prefix}.DependencyTargetNotFound";
        public const string DependencyAlreadyExists  = $"{Prefix}.DependencyAlreadyExists";
        public const string CircularDependency = $"{Prefix}.CircularDependency";

        // Reporting.* — not Dataset.* because this is a Reporting API state, not a Dataset state.
        public const string DataNotReady = "Reporting.DataNotReady";
    }

    public static Error DataNotReady(IReadOnlyList<DatasetReadinessIssue> issues) =>
        Error.Custom(
            type: (int)HttpStatusCode.ServiceUnavailable,
            code: Codes.DataNotReady,
            description: "Data not ready for requested period",
            metadata: new Dictionary<string, object> { ["issues"] = issues });

    public static Error NotFound(Guid id) =>
        Error.NotFound(Codes.NotFound, $"Dataset with ID {id} was not found.");

    public static Error AlreadyExists(string name, string version) =>
        Error.Conflict(Codes.AlreadyExists, $"Dataset '{name}' version '{version}' already exists.");

    public static Error InvalidTransition(DatasetStatus from, DatasetStatus to) =>
        Error.Validation(Codes.InvalidTransition, $"Transition from {from} to {to} is not allowed.");

    public static Error DependenciesNotReady(string? reason) =>
        Error.Validation(Codes.DependenciesNotReady, $"Cannot transition to Ready: {reason}");

    public static Error ArtifactMissing(IReadOnlyList<string> s3Keys) =>
        Error.Validation(Codes.ArtifactMissing,
            $"Artifacts not found in S3: {string.Join(", ", s3Keys.Select(k => $"'{k}'"))}");

    public static Error InvalidStatus(Guid id, DatasetStatus current, DatasetStatus expected) =>
        Error.Validation(Codes.InvalidStatus,
            $"Dataset {id} is in {current} status; expected {expected}.");

    public static Error InvalidStatus(Guid id, DatasetStatus current) =>
        Error.Validation(Codes.InvalidStatus,
            $"Dataset {id} has invalid status {current} for this operation.");

    public static Error DependencyTargetNotFound(Guid dependsOnDatasetId) =>
        Error.NotFound(Codes.DependencyTargetNotFound,
            $"Dependency target dataset with ID {dependsOnDatasetId} was not found.");

    public static Error DependencyAlreadyExists(Guid datasetId, Guid dependsOnDatasetId) =>
        Error.Conflict(Codes.DependencyAlreadyExists,
            $"Dataset {datasetId} already depends on dataset {dependsOnDatasetId}.");

    public static Error CircularDependency(Guid datasetId, Guid dependsOnDatasetId) =>
        Error.Validation(Codes.CircularDependency,
            $"Adding dependency from {datasetId} to {dependsOnDatasetId} would create a circular reference.");
}
