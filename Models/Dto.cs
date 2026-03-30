namespace ContainerManager.Models;

public class CreaContainerRequest
{
    public string Nome { get; set; } = string.Empty;
    public string MigId { get; set; } = string.Empty;
}

public class ContainerResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Stato { get; set; } = string.Empty;
    public string MigUuid { get; set; } = string.Empty;
    public int MigDeviceIndex { get; set; }
    public string MigTipo { get; set; } = string.Empty;
    public double VramGb { get; set; }
    public string DataCreazione { get; set; } = string.Empty;
    public string DataModifica { get; set; } = string.Empty;
}

public class MigSliceResponse
{
    public string Uuid { get; set; } = string.Empty;
    public int DeviceIndex { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public double VramGb { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "")
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message)
        => new() { Success = false, Message = message };
}