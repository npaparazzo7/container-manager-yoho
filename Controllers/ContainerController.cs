using Microsoft.AspNetCore.Mvc;
using ContainerManager.Models;
using ContainerManager.Services;

namespace ContainerManager.Controllers;

[ApiController]
[Route("api/containers")]
public class ContainersController : ControllerBase
{
    private readonly ILogger<ContainersController> _logger;
    private readonly DbService _dbService;
    private readonly ILxcService _lxcService;
    private readonly IMigService _migService;

    public ContainersController(
        ILogger<ContainersController> logger,
        DbService dbService,
        ILxcService lxcService,
        IMigService migService)
    {
        _logger = logger;
        _dbService = dbService;
        _lxcService = lxcService;
        _migService = migService;
    }

    /// <summary>
    /// Restituisce tutti i container attivi (non DELETED).
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<ContainerResponse>>> GetAll()
    {
        var containers = _dbService.GetContainers();
        return Ok(ApiResponse<List<ContainerResponse>>.Ok(MappaLista(containers)));
    }

    /// <summary>
    /// Restituisce solo i container in stato RUNNING.
    /// </summary>
    [HttpGet("attivi")]
    public ActionResult<ApiResponse<List<ContainerResponse>>> GetAttivi()
    {
        var containers = _dbService.GetContainersAttivi();
        return Ok(ApiResponse<List<ContainerResponse>>.Ok(MappaLista(containers)));
    }

    /// <summary>
    /// Restituisce un singolo container per nome.
    /// </summary>
    [HttpGet("{nome}")]
    public ActionResult<ApiResponse<ContainerResponse>> GetByNome(string nome)
    {
        var container = _dbService.GetContainerByNome(nome);

        if (container is null)
            return NotFound(ApiResponse<ContainerResponse>.Fail($"Container '{nome}' non trovato."));

        return Ok(ApiResponse<ContainerResponse>.Ok(Mappa(container)));
    }

    /// <summary>
    /// Crea un nuovo container LXC e lo associa al MIG slice scelto.
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<ContainerResponse>> CreateService([FromBody] CreaContainerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(ApiResponse<ContainerResponse>.Fail("Il nome del container è obbligatorio."));

        if (string.IsNullOrWhiteSpace(request.MigId))
            return BadRequest(ApiResponse<ContainerResponse>.Fail("Il MigUuid è obbligatorio."));

        var esistente = _dbService.GetContainerByNome(request.Nome);
        if (esistente is not null && esistente.Stato != ContainerState.Deleted)
            return Conflict(ApiResponse<ContainerResponse>.Fail($"Container '{request.Nome}' già esistente."));

        var migLiberi = _migService.GetMigSlicesLiberi();
        if(migLiberi.Count == 0)
            return BadRequest(ApiResponse<ContainerResponse>.Fail("Nessun MIG slice disponibile."));

        var mig = migLiberi.FirstOrDefault(m =>
            m.Tipo.Equals(request.MigId, StringComparison.OrdinalIgnoreCase));

        if (mig is null)
            return BadRequest(ApiResponse<ContainerResponse>.Fail(
                $"MIG slice '{request.MigId}' non disponibile o già in uso."));

        var creato = _lxcService.CreaContainer(request.Nome, request.MigId);
        if (!creato)
            return StatusCode(500, ApiResponse<ContainerResponse>.Fail(
                $"Errore durante la creazione del container '{request.Nome}'."));

        var now = DateTime.UtcNow;
        var record = new ContainerRecord
        {
            Nome = request.Nome,
            Stato = ContainerState.Running,
            MigUuid = mig.Uuid,
            MigDeviceIndex = mig.DeviceIndex,
            MigTipo = mig.Tipo,
            VramGb = mig.VramGb,
            DataCreazione = now,
            DataModifica = now
        };

        _dbService.InsertContainer(record);

        _logger.LogInformation("Container '{Nome}' creato con MIG {Uuid}.", request.Nome, mig.Uuid);
        return CreatedAtAction(nameof(GetByNome), new { nome = record.Nome },
            ApiResponse<ContainerResponse>.Ok(Mappa(record), "Container creato con successo."));
    }

    /// <summary>
    /// Elimina un container LXC e libera il MIG slice nel DB.
    /// </summary>
    [HttpDelete("{nome}")]
    public ActionResult<ApiResponse<object>> Elimina(string nome)
    {
        var container = _dbService.GetContainerByNome(nome);
        if (container is null || container.Stato == ContainerState.Deleted)
            return NotFound(ApiResponse<object>.Fail($"Container '{nome}' non trovato."));

        var eliminato = _lxcService.EliminaContainer(nome);
        if (!eliminato)
            return StatusCode(500, ApiResponse<object>.Fail(
                $"Errore durante l'eliminazione del container '{nome}'."));

        _dbService.DeleteContainer(nome);

        _logger.LogInformation("Container '{Nome}' eliminato.", nome);
        return Ok(ApiResponse<object>.Ok(null!, $"Container '{nome}' eliminato con successo."));
    }

    private static ContainerResponse Mappa(ContainerRecord c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Stato = c.Stato,
        MigUuid = c.MigUuid,
        MigDeviceIndex = c.MigDeviceIndex,
        MigTipo = c.MigTipo,
        VramGb = c.VramGb,
        DataCreazione = c.DataCreazione.ToString("o"),
        DataModifica = c.DataModifica.ToString("o")
    };

    private static List<ContainerResponse> MappaLista(List<ContainerRecord> lista)
        => lista.Select(Mappa).ToList();
}