using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TestIdentity.Identity.CustomModel;
using TestIdentity.Identity.DTO;
using TestIdentity.Identity.Stores;

namespace TestIdentity.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICustomSessionStore _sessionStore;

        public AuthController(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            ICustomSessionStore sessionStore)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _sessionStore = sessionStore;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user is null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized();
            }

            await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, user.Permissions);
            return Ok();
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromQuery(Name = "sid")] string sessionId = "")
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                await _signInManager.SignOutAsync();
                return Ok();
            }

            if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                return Unauthorized();
            }

            var removed = await _sessionStore.RemoveOwnedSessionAsync(User.Identity.Name, sessionId);
            if (!removed)
            {
                return NotFound();
            }

            if (string.Equals(User.FindFirstValue(TicketStore.SessionIdClaimType), sessionId, StringComparison.Ordinal))
            {
                await _signInManager.SignOutAsync();
            }

            return Ok();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel user)
        {
            var result = await _userManager.CreateAsync(user.AsAppUser(), user.Password);
            if (result.Succeeded)
            {
                return Ok();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        [AllowAnonymous]
        [HttpGet("me")]
        public IActionResult GetMyInfo()
        {
            return Ok(new CurrentUserResponse
            {
                Name = User.Identity?.Name ?? "Anonymous",
                Roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Permissions = User.FindAll(AppClaimTypes.Permission).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                CurrentSid = User.FindFirstValue(TicketStore.SessionIdClaimType)
            });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<ActionResult<IReadOnlyCollection<SessionInfoResponse>>> GetSessions(CancellationToken cancellationToken)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized();
            }

            var sessions = await _sessionStore.GetSessionsAsync(username, cancellationToken);
            var response = sessions.Select(session => new SessionInfoResponse
            {
                SessionId = session.Properties.GetString(TicketStore.SessionIdPropertyName),
                AuthenticationScheme = session.AuthenticationScheme,
                IssuedUtc = session.Properties.IssuedUtc,
                ExpiresUtc = session.Properties.ExpiresUtc,
                IsPersistent = session.Properties.IsPersistent,
                AllowRefresh = session.Properties.AllowRefresh
            }).ToArray();

            return Ok(response);
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized();
            }

            await _sessionStore.RemoveAllAsync(username, cancellationToken);
            await _signInManager.SignOutAsync();

            return Ok();
        }
    }
}
