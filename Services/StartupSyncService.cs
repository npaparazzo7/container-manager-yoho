using ContainerManager.Models;

namespace ContainerManager.Services;

public class StartupSyncService
{
    private readonly ILogger<StartupSyncService> _logger;
    private readonly DbService _dbService;
    private readonly ILxcService _lxcService;
    private readonly IMigService _migService;

    public StartupSyncService(
        ILogger<StartupSyncService> logger,
        DbService dbService,
        ILxcService lxcService,
        IMigService migService)
    {
        _logger = logger;
        _dbService = dbService;
        _lxcService = lxcService;
        _migService = migService;
    }

    public void Sincronizza()
    {
        _logger.LogInformation("=== Avvio sincronizzazione ===");

        _dbService.InitializeDb();

        SincronizzaStatiLxc();
        SincronizzaUuidMig();

        _logger.LogInformation("=== Sincronizzazione completata ===");
    }


    private void SincronizzaStatiLxc()
    {
        _logger.LogInformation("Sincronizzazione stati LXC...");

        var containersLxc = _lxcService.GetContainers();
        var containersDb = _dbService.GetContainers();

        var nomiLxc = containersLxc
            .Select(c => c.Nome)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var containerDb in containersDb)
        {
            var lxc = containersLxc.FirstOrDefault(c =>
                c.Nome.Equals(containerDb.Nome, StringComparison.OrdinalIgnoreCase));

            if (lxc is null)
            {
                _logger.LogWarning(
                    "Container '{Nome}' non trovato in LXC",
                    containerDb.Nome);

                _dbService.DeleteContainer(containerDb.Nome);
                continue;
            }

            if (lxc.Stato != containerDb.Stato)
            {
                _logger.LogInformation(
                    "Container '{Nome}': stato aggiornato {Old} → {New}.",
                    containerDb.Nome, containerDb.Stato, lxc.Stato);

                _dbService.UpdateStatoContainer(containerDb.Nome, lxc.Stato);
            }
        }
    }

    private void SincronizzaUuidMig()
    {
        _logger.LogInformation("Sincronizzazione UUID MIG slice...");

        var migAttuali = _migService.GetMigSlices();
        var containersDb = _dbService.GetContainers();

        foreach (var container in containersDb)
        {
            var migCorrispondente = migAttuali
                .FirstOrDefault(m => m.Tipo == container.MigTipo);

            if (migCorrispondente is null)
            {
                _logger.LogWarning(
                    "Nessun MIG trovato di tipo {Tipo} (container: '{Nome}'). " +
                    "La GPU potrebbe essere stata riconfigurata.",
                    container.MigTipo, container.Nome);
                continue;
            }

            if (migCorrispondente.Uuid != container.MigUuid)
            {
                _logger.LogInformation(
                    "Container '{Nome}' — UUID MIG aggiornato: {Old} → {New}.",
                    container.Nome, container.MigUuid, migCorrispondente.Uuid);

                _dbService.UpdateMigUuid(container.Nome, migCorrispondente.Uuid);
            }
            else
            {
                _logger.LogInformation(
                    "Container '{Nome}' — UUID MIG invariato, nessun aggiornamento necessario.",
                    container.Nome);
            }
        }
    }
}