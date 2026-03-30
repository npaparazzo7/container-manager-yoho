using System.Net;
using System.Text.Json;

namespace ContainerManager.Middleware;

public class IpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpWhitelistMiddleware> _logger;
    private readonly string _whitelistFilePath;
    private HashSet<string> _allowedIps = [];
    private FileSystemWatcher? _watcher;

    public IpWhitelistMiddleware(
        RequestDelegate next,
        ILogger<IpWhitelistMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _whitelistFilePath = configuration["AppSettings:IpWhitelistPath"]
            ?? "ip-whitelist.json";

        CaricaIp();
        AvviaWatcher();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Escludi Scalar e OpenAPI
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/scalar") || path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        var ipRemoto = context.Connection.RemoteIpAddress;

        if (ipRemoto is null)
        {
            _logger.LogWarning("Richiesta con IP nullo — bloccata.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("IP non valido.");
            return;
        }

        // Normalizza IPv6 mapped IPv4 (es. ::ffff:127.0.0.1 → 127.0.0.1)
        if (ipRemoto.IsIPv4MappedToIPv6)
            ipRemoto = ipRemoto.MapToIPv4();

        var ipString = ipRemoto.ToString();

        if (!_allowedIps.Contains(ipString))
        {
            _logger.LogWarning("IP non autorizzato: {IP}.", ipString);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("IP non autorizzato.");
            return;
        }

        _logger.LogInformation("IP autorizzato: {IP}.", ipString);
        await _next(context);
    }

    private void CaricaIp()
    {
        try
        {
            if (!File.Exists(_whitelistFilePath))
            {
                _logger.LogWarning("File whitelist non trovato: {Path}. Creo file vuoto.", _whitelistFilePath);
                File.WriteAllText(_whitelistFilePath, JsonSerializer.Serialize(new { AllowedIPs = Array.Empty<string>() }));
                _allowedIps = [];
                return;
            }

            var json = File.ReadAllText(_whitelistFilePath);
            var doc = JsonDocument.Parse(json);
            var ips = doc.RootElement
                .GetProperty("AllowedIPs")
                .EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .ToHashSet();

            _allowedIps = ips;
            _logger.LogInformation("Whitelist aggiornata — {Count} IP autorizzati.", _allowedIps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il caricamento della whitelist IP.");
        }
    }

    private void AvviaWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_whitelistFilePath)) ?? ".";
            var fileName = Path.GetFileName(_whitelistFilePath);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _logger.LogInformation("FileSystemWatcher avviato su '{Path}'.", _whitelistFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'avvio del FileSystemWatcher.");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Piccolo delay di sicurezza
        Thread.Sleep(200);
        _logger.LogInformation("Whitelist modificata — ricarico IP...");
        CaricaIp();
    }
}