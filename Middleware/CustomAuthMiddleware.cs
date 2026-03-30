using System.Security.Cryptography;
using System.Text;

namespace ContainerManager.Middleware;

public class HmacAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacAuthMiddleware> _logger;
    private readonly string _secretKey;

    // Finestra temporale accettata — previene replay attack
    private const int ToleranceTime = 30;

    public HmacAuthMiddleware(
        RequestDelegate next,
        ILogger<HmacAuthMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _secretKey = configuration["AppSettings:HmacSecretKey"]
            ?? throw new InvalidOperationException("HmacSecretKey non configurata.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Escludi Scalar e OpenAPI dall'autenticazione
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/scalar") || path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        // 1. Leggi gli header
        if (!context.Request.Headers.TryGetValue("X-Timestamp", out var timestampStr) ||
            !context.Request.Headers.TryGetValue("X-Signature", out var signatureRicevuta))
        {
            _logger.LogWarning("Richiesta senza header HMAC da {IP}.", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Header di autenticazione mancanti.");
            return;
        }

        // 2. Valida il timestamp — previene replay attack
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Timestamp non valido.");
            return;
        }

        var ora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var differenza = Math.Abs(ora - timestamp);

        if (differenza > ToleranceTime)
        {
            _logger.LogWarning("Timestamp scaduto — differenza: {Diff}s.", differenza);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Timestamp scaduto.");
            return;
        }

        // 3. Leggi il body
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        // 4. Calcola la firma attesa
        var metodo = context.Request.Method.ToUpper();
        var percorso = context.Request.Path + context.Request.QueryString;
        var payload = $"{metodo}\n{percorso}\n{timestamp}\n{body}";

        var firmaAttesa = CalcolaHmac(payload, _secretKey);

        // 5. Confronta le firme
        if (!CryptoCompare(firmaAttesa, signatureRicevuta!))
        {
            _logger.LogWarning("Firma HMAC non valida per {Metodo} {Path}.", metodo, percorso);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Firma non valida.");
            return;
        }

        _logger.LogInformation("Richiesta autenticata: {Metodo} {Path}.", metodo, percorso);
        await _next(context);
    }

    private static string CalcolaHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLower();
    }

    private static bool CryptoCompare(string a, string b)
    {
        // Confronto a tempo costante — previene timing attack
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}