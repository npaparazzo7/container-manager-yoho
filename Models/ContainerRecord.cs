namespace ContainerManager.Models
{
    public class ContainerRecord
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Stato { get; set; } = string.Empty;
        public string MigUuid { get; set; } = string.Empty;
        public int MigDeviceIndex { get; set; }
        public string MigTipo { get; set; } = string.Empty;
        public double VramGb { get; set; }
        public DateTime DataCreazione { get; set; }
        public DateTime DataModifica { get; set; }
    }
}
