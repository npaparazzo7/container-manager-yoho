namespace ContainerManager.Models;

public class LxcContainer
{
    public string Nome { get; set; } = string.Empty;
    public string Stato { get; set; } = string.Empty;
    public string Ipv4 { get; set; } = string.Empty;
}