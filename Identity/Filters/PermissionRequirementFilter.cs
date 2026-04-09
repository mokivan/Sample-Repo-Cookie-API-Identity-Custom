using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TestIdentity.Identity.CustomModel;

namespace TestIdentity.Identity.Filters
{
    public sealed class PermissionAuthorizationRequirement : IAuthorizationRequirement
    {
        public PermissionAuthorizationRequirement(IEnumerable<string> permissions)
        {
            Permissions = permissions
                .Select(permission => permission.Trim())
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyCollection<string> Permissions { get; }
    }

    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionAuthorizationRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return Task.CompletedTask;
            }

            var permissions = context.User.FindAll(AppClaimTypes.Permission).Select(claim => claim.Value);
            if (permissions.Intersect(requirement.Permissions, StringComparer.OrdinalIgnoreCase).Any())
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public const string PolicyPrefix = "permission:";

        public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
        }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return base.GetPolicyAsync(policyName);
            }

            var permissionPayload = policyName[PolicyPrefix.Length..];
            var permissions = permissionPayload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionAuthorizationRequirement(permissions))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        public static string BuildPolicyName(string permissions)
        {
            return $"{PolicyPrefix}{permissions}";
        }
    }
}
