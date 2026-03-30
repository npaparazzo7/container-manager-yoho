using Microsoft.AspNetCore.Mvc;
using ContainerManager.Models;
using ContainerManager.Services;

namespace ContainerManager.Controllers;

[ApiController]
[Route("api/mig")]
public class MigController : ControllerBase
{
    private readonly ILogger<MigController> _logger;
    private readonly IMigService _migService;

    public MigController(ILogger<MigController> logger, IMigService migService)
    {
        _logger = logger;
        _migService = migService;
    }

    /// <summary>
    /// Restituisce tutti i MIG slice rilevati sulla GPU.
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<MigSliceResponse>>> GetAll()
    {
        var slices = _migService.GetMigSlices();

        var response = slices.Select(m => new MigSliceResponse
        {
            Uuid = m.Uuid,
            DeviceIndex = m.DeviceIndex,
            Tipo = m.Tipo,
            VramGb = m.VramGb
        }).ToList();

        return Ok(ApiResponse<List<MigSliceResponse>>.Ok(response));
    }

    /// <summary>
    /// Restituisce solo i MIG slice liberi (non assegnati a nessun container).
    /// </summary>
    [HttpGet("liberi")]
    public ActionResult<ApiResponse<List<MigSliceResponse>>> GetLiberi()
    {
        var slices = _migService.GetMigSlicesLiberi();

        if (slices.Count == 0)
            return Ok(ApiResponse<List<MigSliceResponse>>.Fail("Nessun MIG slice disponibile."));

        var response = slices.Select(m => new MigSliceResponse
        {
            Uuid = m.Uuid,
            DeviceIndex = m.DeviceIndex,
            Tipo = m.Tipo,
            VramGb = m.VramGb
        }).ToList();

        return Ok(ApiResponse<List<MigSliceResponse>>.Ok(response));
    }
}