using System;

namespace Constellate.Persistence.Archive
{
    public sealed record ActionInvocationRecord(
        string ActionInvocationId,
        string ActionId,
        string ActionClass,
        string OwnerKind,
        string TargetDomain,
        string TargetRef,
        string InvocationChannel,
        string ResultState,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt = null,
        string? CorrelationId = null,
        string? FailureSummary = null);
}
