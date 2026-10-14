using Microsoft.AspNetCore.Mvc;
using Sonrisa.EventAlertSystem.Api.Application;

namespace Sonrisa.EventAlertSystem.Api.Controllers;

[ApiController]
[Route("api/demo-identities")]
public sealed class DemoIdentitiesController(DemoIdentityRegistry identityRegistry) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DemoIdentityResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DemoIdentityResponse>> GetAll()
    {
        var identities = identityRegistry.GetAll()
            .Select(identity => new DemoIdentityResponse(identity.Id, identity.DisplayName, identity.Role))
            .ToArray();

        return Ok(identities);
    }

    public sealed record DemoIdentityResponse(string Id, string DisplayName, string Role);
}