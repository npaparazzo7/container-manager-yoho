using ContainerManager.Models;

namespace ContainerManager.Services;

public interface ILxcService
{
    List<LxcContainer> GetContainers();
    LxcContainer? GetContainerByNome(string nome);
    bool CreaContainer(string nome, string migUuid);
    bool EliminaContainer(string nome);
}