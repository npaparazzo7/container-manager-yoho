namespace ContainerManager.Models
{
    public class MigSlice
    {
        public string Uuid { get; set; } = string.Empty;
        public int DeviceIndex { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public double VramGb { get; set; }
        public bool InUso { get; set; }
    }
}
