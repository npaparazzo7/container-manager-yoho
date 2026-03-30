using ContainerManager.Models;
using System.Text.RegularExpressions;

namespace ContainerManager.Services;

public class MigService : IMigService
{
    private readonly ILogger<MigService> _logger;
    private readonly DbService _dbService;
    private readonly ShellService _shell;

    public MigService(ILogger<MigService> logger, DbService dbService, ShellService shell)
    {
        _logger = logger;
        _dbService = dbService;
        _shell = shell;
    }

    public List<MigSlice> GetMigSlices()
    {
        var result = _shell.Esegui("nvidia-smi", "-L");
        if (!result.Success) return [];
        return ParseMigSlices(result.Output);
    }

    public List<MigSlice> GetMigSlicesLiberi()
    {
        var tutti = GetMigSlices();
        var containersAttivi = _dbService.GetContainers();
        var uuidInUso = containersAttivi.Select(c => c.MigUuid).ToHashSet();

        return tutti.Where(m => !uuidInUso.Contains(m.Uuid)).ToList();
    }

    public void SyncDopoReboot()
    {
        _logger.LogInformation("Avvio sincronizzazione MIG slice dopo reboot...");

        var migAttuali = GetMigSlices();
        var containersNelDb = _dbService.GetContainers();

        foreach (var container in containersNelDb)
        {
            // Cerca un MIG con lo stesso device_index
            var migCorrispondente = migAttuali
                .FirstOrDefault(m => m.DeviceIndex == container.MigDeviceIndex);

            if (migCorrispondente is null)
            {
                _logger.LogWarning(
                    "Nessun MIG trovato per device_index {Index} (container: {Nome}). Potrebbe essere stato rimosso.",
                    container.MigDeviceIndex, container.Nome);
                continue;
            }

            // Aggiorna UUID se cambiato
            if (migCorrispondente.Uuid != container.MigUuid)
            {
                _logger.LogInformation(
                    "UUID aggiornato per device_index {Index}: {OldUuid} → {NewUuid}",
                    container.MigDeviceIndex, container.MigUuid, migCorrispondente.Uuid);

                _dbService.UpdateMigUuid(container.Nome, migCorrispondente.Uuid);
            }
        }

        _logger.LogInformation("Sincronizzazione MIG completata.");
    }

    // ─── PARSING ─────────────────────────────────────────────────────────────

    private List<MigSlice> ParseMigSlices(string output)
    {
        var result = new List<MigSlice>();

        // Esempio riga da parsare:
        // MIG 2g.12gb     Device 0: (UUID: MIG-05ac6abb-b127-53a3-b247-7d4aa0c11fe2)
        // MIG 1g.6gb      Device 1: (UUID: MIG-8b7a8d15-c2a1-549c-abe9-a7c2c0d18f7e)

        var regex = new Regex(
            @"MIG\s+(?<tipo>\d+g\.\d+gb)\s+Device\s+(?<index>\d+):\s+\(UUID:\s+(?<uuid>MIG-[a-f0-9\-]+)\)",
            RegexOptions.IgnoreCase);

        foreach (var line in output.Split('\n'))
        {
            var match = regex.Match(line.Trim());
            if (!match.Success) continue;

            var tipo = match.Groups["tipo"].Value.ToLower();
            var deviceIndex = int.Parse(match.Groups["index"].Value);
            var uuid = match.Groups["uuid"].Value;

            result.Add(new MigSlice
            {
                Uuid = uuid,
                DeviceIndex = deviceIndex,
                Tipo = tipo,
                VramGb = ParseVramDaTipo(tipo),
                InUso = false // verrà calcolato in GetMigSlicesLiberi()
            });
        }

        _logger.LogInformation("Rilevati {Count} MIG slice.", result.Count);
        return result;
    }

    private static double ParseVramDaTipo(string tipo)
    {
        // es. "2g.12gb" → 12.0
        //     "1g.6gb"  → 6.0
        var match = Regex.Match(tipo, @"(\d+)gb", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, out var gb))
            return gb;

        return 0;
    }
}