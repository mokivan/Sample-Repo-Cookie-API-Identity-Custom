using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace TestIdentity.Controllers
{
    [Route("api/test-role")]
    [ApiController]
    [Authorize(Roles = "Superuser")]
    public class AuthorizationByRoleController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                Status = HttpStatusCode.OK,
                Message = "Endpoint `test-role` is working"
            });
        }
    }
}
