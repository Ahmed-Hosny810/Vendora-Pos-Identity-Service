
namespace Pos.Identity.Application.Dtos
{
    public class TenantSubscriptionAccessDto
    {
        public Guid TenantId { get; set; }

        public bool IsAllowed { get; set; }

        public DateTime? SubscriptionEndsAt { get; set; }

        public DateTime CheckedAtUtc { get; set; }

        public string? DenialReason { get; set; }
    }
}
