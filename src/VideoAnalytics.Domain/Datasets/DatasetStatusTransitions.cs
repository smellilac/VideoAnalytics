namespace VideoAnalytics.Domain.Datasets;

public static class DatasetStatusTransitions
{
    private static readonly IReadOnlyDictionary<DatasetStatus, IReadOnlySet<DatasetStatus>> _allowed =
        new Dictionary<DatasetStatus, IReadOnlySet<DatasetStatus>>
        {
            [DatasetStatus.Pending] = new HashSet<DatasetStatus> { DatasetStatus.InProgress },
            [DatasetStatus.InProgress] = new HashSet<DatasetStatus> { DatasetStatus.Ready, DatasetStatus.Failed },
            [DatasetStatus.Failed] = new HashSet<DatasetStatus> { DatasetStatus.Pending },
            [DatasetStatus.Ready] = new HashSet<DatasetStatus>(),
        };

    public static bool IsAllowed(DatasetStatus from, DatasetStatus to) =>
        _allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
