using System.ComponentModel.DataAnnotations;
using TestIdentity.Identity.CustomModel;

namespace TestIdentity.Identity.DTO
{
    public class RegisterModel
    {
        [Required]
        [StringLength(128, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;

        public List<int> Roles { get; set; } = new();

        public AppUser AsAppUser()
        {
            return new AppUser
            {
                Name = Name.Trim(),
                Email = Email.Trim(),
                EmailConfirmed = true,
                Username = Username.Trim(),
                Roles = Roles.Select(roleId => new AppRole { Id = roleId }).ToList()
            };
        }
    }
}
