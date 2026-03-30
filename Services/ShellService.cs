using System.Diagnostics;

namespace ContainerManager.Services;

public class ShellService
{
    private readonly ILogger<ShellService> _logger;

    public ShellService(ILogger<ShellService> logger)
    {
        _logger = logger;
    }

    public ShellResult Esegui(string comando, string argomenti)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = comando,
                    Arguments = argomenti,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                _logger.LogError("Errore [{ExitCode}] eseguendo '{Comando} {Argomenti}': {Error}",
                    process.ExitCode, comando, argomenti, error);

            return new ShellResult
            {
                Output = output,
                Error = error,
                ExitCode = process.ExitCode,
                Success = process.ExitCode == 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eccezione eseguendo '{Comando} {Argomenti}'.", comando, argomenti);
            return new ShellResult
            {
                Output = string.Empty,
                Error = ex.Message,
                ExitCode = -1,
                Success = false
            };
        }
    }
}

public class ShellResult
{
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool Success { get; set; }
}