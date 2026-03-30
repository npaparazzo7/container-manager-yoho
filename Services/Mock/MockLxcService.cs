using ContainerManager.Models;

namespace ContainerManager.Services.Mock;

public class MockLxcService : ILxcService
{
    private readonly List<LxcContainer> _containers = [];
    private readonly ILogger<MockLxcService> _logger;

    public MockLxcService(ILogger<MockLxcService> logger)
    {
        _logger = logger;
    }

    public List<LxcContainer> GetContainers() => _containers;

    public LxcContainer? GetContainerByNome(string nome)
        => _containers.FirstOrDefault(c =>
            c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

    public bool CreaContainer(string nome, string migUuid)
    {
        _logger.LogInformation("[MOCK] Creazione container '{Nome}' con MIG {Uuid}.", nome, migUuid);
        _containers.Add(new LxcContainer
        {
            Nome = nome,
            Stato = ContainerState.Running,
            Ipv4 = "10.0.0.1"
        });
        return true;
    }

    public bool EliminaContainer(string nome)
    {
        _logger.LogInformation("[MOCK] Eliminazione container '{Nome}'.", nome);
        var container = _containers.FirstOrDefault(c =>
            c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

        if (container is null) return false;
        _containers.Remove(container);
        return true;
    }
}