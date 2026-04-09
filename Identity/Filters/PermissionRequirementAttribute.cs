using Microsoft.AspNetCore.Authorization;

namespace TestIdentity.Identity.Filters
{
    public class PermissionRequirementAttribute : AuthorizeAttribute
    {
        public PermissionRequirementAttribute(string permission)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permission);
            Policy = PermissionAuthorizationPolicyProvider.BuildPolicyName(permission);
        }
    }
}
