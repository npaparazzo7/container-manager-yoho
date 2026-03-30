using ContainerManager.Models;

namespace ContainerManager.Services.Mock;

public class MockMigService : IMigService
{
    private readonly List<MigSlice> _slices =
    [
        new() { Uuid = "MIG-aaaa-1111", DeviceIndex = 0, Tipo = "2g.12gb", VramGb = 12 },
        new() { Uuid = "MIG-bbbb-2222", DeviceIndex = 1, Tipo = "1g.6gb",  VramGb = 6  },
        new() { Uuid = "MIG-cccc-3333", DeviceIndex = 2, Tipo = "1g.6gb",  VramGb = 6  }
    ];

    private readonly DbService _dbService;

    public MockMigService(DbService dbService)
    {
        _dbService = dbService;
    }

    public List<MigSlice> GetMigSlices() => _slices;

    public List<MigSlice> GetMigSlicesLiberi()
    {
        var inUso = _dbService.GetContainers()
            .Select(c => c.MigUuid)
            .ToHashSet();

        return _slices.Where(m => !inUso.Contains(m.Uuid)).ToList();
    }

    public void SyncDopoReboot()
    {
        // mock — non fa nulla
    }
}