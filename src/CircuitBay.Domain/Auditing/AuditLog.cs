using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Auditing;

public sealed class AuditLog : BaseEntity
{
    private AuditLog() { }

    public AuditLog(Guid? actorId, string action, string entityName, Guid? entityId, string summary)
    {
        ActorId = actorId;
        Action = action.Trim();
        EntityName = entityName.Trim();
        EntityId = entityId;
        Summary = summary.Trim();
    }

    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
}
