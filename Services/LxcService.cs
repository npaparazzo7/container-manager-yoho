using ContainerManager.Models;
using System.Text.Json;
using System.Xml.Linq;

namespace ContainerManager.Services;

public class LxcService : ILxcService
{
    private readonly ILogger<LxcService> _logger;
    private readonly ShellService _shell;

    public LxcService(ILogger<LxcService> logger, ShellService shell)
    {
        _logger = logger;
        _shell = shell;
    }

    public List<LxcContainer> GetContainers()
    {
        var result = _shell.Esegui("lxc", "list --format json");
        if (!result.Success)
        {
            _logger.LogError("Impossibile ottenere la lista dei container LXC.");
            return [];
        }

        return ParseContainers(result.Output);
    }

    public LxcContainer? GetContainerByNome(string nome)
    {
        return GetContainers().FirstOrDefault(c =>
            c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
    }

    public bool CreaContainer(string nome, string migUuid)
    {
        _logger.LogInformation("Creazione container LXC '{Nome}' con MIG {Uuid}...", nome, migUuid);

        // 1. Lancia il container da un'immagine base Ubuntu
        var launch = _shell.Esegui("lxc", $"launch ubuntu:24.04 {nome}");
        if (!launch.Success)
        {
            _logger.LogError("Errore durante lxc launch per '{Nome}'.", nome);
            return false;
        }

        // 2. Assegna il MIG slice al container tramite GPU passthrough
        var gpu = _shell.Esegui("lxc",
            $"config device add {nome} gpu gpu id={migUuid}");
        if (!gpu.Success)
        {
            _logger.LogError("Errore durante l'assegnazione del MIG a '{Nome}'. Rimozione container...", nome);
            EliminaContainer(nome);
            return false;
        }

        _logger.LogInformation("Container '{Nome}' creato e MIG assegnato.", nome);
        return true;
    }

    public bool EliminaContainer(string nome)
    {
        _logger.LogInformation("Eliminazione container LXC '{Nome}'...", nome);

        // Prima va fermato, poi eliminato
        var stop = _shell.Esegui("lxc", $"stop {nome} --force");
        if (!stop.Success)
            _logger.LogWarning("Impossibile fermare '{Nome}', si tenta comunque l'eliminazione.", nome);

        var delete = _shell.Esegui("lxc", $"delete {nome} --force");
        if (!delete.Success)
        {
            _logger.LogError("Errore durante lxc delete per '{Nome}'.", nome);
            return false;
        }

        _logger.LogInformation("Container '{Nome}' eliminato.", nome);
        return true;
    }

    private List<LxcContainer> ParseContainers(string json)
    {
        // lxc list --format json restituisce un array di oggetti
        // Esempio:
        // [
        //   {
        //     "name": "ai-builder",
        //     "status": "Running",
        //     "state": { ... },
        //     "network": { "eth0": { "addresses": [ { "address": "10.251.110.33" } ] } }
        //   }
        // ]

        try
        {
            var result = new List<LxcContainer>();
            var jsonDoc = JsonDocument.Parse(json);
            var containers = jsonDoc.RootElement.EnumerateArray();

            foreach (var c in containers)
            {
                var nome = c.GetProperty("name").GetString() ?? string.Empty;
                var stato = c.GetProperty("status").GetString() ?? string.Empty;
                var ipv4 = EstraiIpv4(c);

                result.Add(new LxcContainer
                {
                    Nome = nome,
                    Stato = NormalizzaStato(stato),
                    Ipv4 = ipv4
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il parsing dell'output di lxc list.");
            return [];
        }
    }

    private static string EstraiIpv4(JsonElement container)
    {
        try
        {
            var network = container.GetProperty("network");
            foreach (var iface in network.EnumerateObject())
            {
                var addresses = iface.Value.GetProperty("addresses").EnumerateArray();
                foreach (var addr in addresses)
                {
                    if (addr.GetProperty("family").GetString() == "inet")
                        return addr.GetProperty("address").GetString() ?? string.Empty;
                }
            }
        }
        catch { /* network potrebbe non essere disponibile */ }

        return string.Empty;
    }

    private static string NormalizzaStato(string statoLxc)
    {
        // LXC usa "Running", "Stopped" — noi usiamo uppercase
        return statoLxc.ToUpper() switch
        {
            "RUNNING" => ContainerState.Running,
            "STOPPED" => ContainerState.Stopped,
            _ => ContainerState.Stopped
        };
    }
}