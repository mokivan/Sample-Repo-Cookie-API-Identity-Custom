namespace TestIdentity.Identity.DTO
{
    public sealed class CurrentUserResponse
    {
        public string Name { get; init; } = string.Empty;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
        public bool IsAuthenticated { get; init; }
        public string? CurrentSid { get; init; }
    }
}
