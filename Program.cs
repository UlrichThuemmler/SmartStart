namespace SmartStart;

internal static class Program
{
    private const string MutexName = "Local\\SmartStart.SingleInstance";
    public const string ShowEventName = "Local\\SmartStart.Show";
    public const string QuitEventName = "Local\\SmartStart.Quit";

    public static string? SelfTestDir { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        string configPath = AppSettings.DefaultPath;
        bool autostart = false;
        bool quit = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--config" when i + 1 < args.Length:
                    configPath = Path.GetFullPath(args[++i]);
                    break;
                case "--log" when i + 1 < args.Length:
                    Log.FilePath = Path.GetFullPath(args[++i]);
                    break;
                case "--selftest" when i + 1 < args.Length:
                    SelfTestDir = Path.GetFullPath(args[++i]);
                    autostart = true;
                    break;
                case "--autostart":
                    autostart = true;
                    break;
                case "--quit":
                    quit = true;
                    break;
            }
        }

        using var mutex = new Mutex(true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            // Läuft schon: dort das Menü öffnen (bzw. beenden)
            Native.AllowSetForegroundWindow(-1);
            try
            {
                using var ev = EventWaitHandle.OpenExisting(quit ? QuitEventName : ShowEventName);
                ev.Set();
            }
            catch
            {
                // Instanz beendet sich gerade
            }

            return;
        }

        if (quit)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp(configPath, autostart));
        GC.KeepAlive(mutex);
    }
}
