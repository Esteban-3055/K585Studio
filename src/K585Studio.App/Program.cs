using K585Studio.Core;
using System.Text.Json;

namespace K585Studio.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--ui-self-test")
        {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm(false);
            return form.RunUiChecks(Path.GetFullPath(args[1]));
        }
        using var instance = new Mutex(true, "Local\\K585Studio.v01", out bool first);
        if (!first) { MessageBox.Show("K585 Studio ya está abierto.", "K585 Studio"); return 2; }
        if (args.Contains("--read-only-diagnostics")) return Diagnose();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int Diagnose()
    {
        try
        {
            using var session = new KeyboardSession(NativeHid.Open(), Storage.Backup);
            var first = session.RefreshAsync().GetAwaiter().GetResult();
            var second = session.RefreshAsync().GetAwaiter().GetResult();
            if (!first.SameData(second)) throw new IOException("La configuración cambió entre lecturas.");
            var report = new { app = "0.1.0", readOnly = true, independentlyReadSnapshots = 4, unchanged = true,
                second.ActiveProfile, lighting = second.Profile(2), digest = second.Digest, checkedAt = DateTimeOffset.UtcNow };
            Directory.CreateDirectory(Storage.Root);
            File.WriteAllText(Path.Combine(Storage.Root, "diagnostics.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Storage.Root);
            File.WriteAllText(Path.Combine(Storage.Root, "diagnostics-error.txt"), ex.ToString());
            return 1;
        }
    }
}
