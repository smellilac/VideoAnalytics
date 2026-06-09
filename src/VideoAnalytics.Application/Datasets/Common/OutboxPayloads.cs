namespace VideoAnalytics.Application.Datasets.Common;

public record StatusChangedPayload(Guid DatasetId, string FromStatus, string ToStatus);
public record DatasetReadyPayload(Guid DatasetId);
