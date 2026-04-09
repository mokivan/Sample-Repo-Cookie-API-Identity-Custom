using System.ComponentModel.DataAnnotations;

namespace TestIdentity.Identity.DTO
{
    public class LoginModel
    {
        [Required(ErrorMessage = "User name or email is required.")]
        [StringLength(128)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(256)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
