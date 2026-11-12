namespace SmartStart;

/// <summary>Optionales Diagnose-Log (nur aktiv mit --log &lt;datei&gt;).</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string? FilePath { get; set; }

    public static void Write(string message)
    {
        if (FilePath == null)
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // ignorieren
            }
        }
    }
}
