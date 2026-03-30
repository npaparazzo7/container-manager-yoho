using ContainerManager.Models;

namespace ContainerManager.Services;

public interface IMigService
{
    List<MigSlice> GetMigSlices();
    List<MigSlice> GetMigSlicesLiberi();
    void SyncDopoReboot();
}