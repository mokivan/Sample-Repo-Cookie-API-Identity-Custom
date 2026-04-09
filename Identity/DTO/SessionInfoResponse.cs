namespace TestIdentity.Identity.DTO
{
    public sealed class SessionInfoResponse
    {
        public string? SessionId { get; init; }
        public string AuthenticationScheme { get; init; } = string.Empty;
        public DateTimeOffset? IssuedUtc { get; init; }
        public DateTimeOffset? ExpiresUtc { get; init; }
        public bool IsPersistent { get; init; }
        public bool? AllowRefresh { get; init; }
    }
}
