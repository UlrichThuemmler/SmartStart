namespace SmartStart;

/// <summary>
/// Diagnose-Log. <see cref="Write"/> schreibt nur mit --log &lt;datei&gt;;
/// <see cref="Error"/> schreibt immer – ohne --log nach %APPDATA%\SmartStart\error.log.
/// </summary>
internal static class Log
{
    private const long MaxErrorLogBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static string? FilePath { get; set; }

    public static string ErrorLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartStart", "error.log");

    public static void Write(string message)
    {
        if (FilePath != null)
        {
            Append(FilePath, message);
        }
    }

    /// <summary>Fehler immer protokollieren, damit sich Probleme später nachvollziehen lassen.</summary>
    public static void Error(string message)
    {
        string path = FilePath ?? ErrorLogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var info = new FileInfo(path);
            if (FilePath == null && info.Exists && info.Length > MaxErrorLogBytes)
            {
                File.Move(path, path + ".old", true);
            }
        }
        catch
        {
            // ignorieren
        }

        Append(path, $"ERROR (v{Version}) {message}");
    }

    private static string Version =>
        typeof(Log).Assembly.GetName().Version?.ToString(3) ?? "?";

    private static void Append(string path, string message)
    {
        lock (Gate)
        {
            try
            {
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // ignorieren
            }
        }
    }
}
