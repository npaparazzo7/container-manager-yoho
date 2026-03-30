using ContainerManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.ComponentModel.Design;
using System.Text.Json.Serialization;

namespace ContainerManager.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class CmdTestController : ControllerBase
    {
        private readonly ShellService _shell;

        public CmdTestController(ShellService shell)
        {
            _shell = shell;
        }

        [HttpGet("shell")]
        public ActionResult EseguiTest()
        {
            var result = _shell.Esegui("cmd.exe", "/c dir");
            return Ok(new
            {
                result.Success,
                result.ExitCode,
                result.Output,
                result.Error
            });
        }
    }
}
