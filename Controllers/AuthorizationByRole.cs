using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace TestIdentity.Controllers
{
    [Route("api/test-role-dont-exists")]
    [ApiController]
    [Authorize(Roles = "DoesNotExist")]
    public class AuthorizationByMissingRoleController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                Status = HttpStatusCode.OK,
                Message = "Endpoint `test-role-dont-exists` works"
            });
        }
    }
}
