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
        if (!first) { if(args.Length==0)MessageBox.Show("K585 Studio ya está abierto.", "K585 Studio"); return 2; }
        if (args.Contains("--read-only-diagnostics")) return Diagnose();
        if (args.Contains("--hardware-self-test")) return HardwareCheck();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int HardwareCheck()
    {
        string reportPath=Path.Combine(Storage.Root,"hardware-checks-v02.json");
        var checks=new List<string>();
        void Save(string stage,bool restored,string? error=null)
        {
            Directory.CreateDirectory(Storage.Root);
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{version=Updates.Current.ToString(),stage,restored,checks,error,utc=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
        }
        try
        {
            using var session=new KeyboardSession(NativeHid.Open(),Storage.Backup);
            var baseline=session.RefreshAsync().GetAwaiter().GetResult();
            if(baseline.ActiveProfile!=2)throw new InvalidOperationException("La prueba requiere el perfil 2 ya activo.");
            var light=baseline.Profile(2);light.Validate();var binding=BindingCatalog.Read(baseline,2,"G5");binding.Validate();int rate=baseline.Illumination[79];
            if(rate>3)throw new InvalidDataException("Frecuencia no reconocida.");
            Save("baseline-backed-up",true);
            Save("testing-profile2-brightness",false);
            session.ApplyLightingAsync(2,light with{Brightness=(byte)(light.Brightness==4?3:light.Brightness+1)},true).GetAwaiter().GetResult();
            session.ApplyLightingAsync(2,light,true).GetAwaiter().GetResult();
            if(!baseline.SameData(session.Current!))throw new IOException("La restauración de brillo no coincide.");checks.Add("Brightness write/readback and full restoration");Save("brightness-restored",true);
            Save("testing-profile2-G5",false);
            var testBinding=KeyBinding.Keyboard(binding==KeyBinding.Keyboard(0x73)?(byte)0x72:(byte)0x73);
            session.ApplyBindingAsync(2,"G5",testBinding).GetAwaiter().GetResult();
            session.ApplyBindingAsync(2,"G5",binding).GetAwaiter().GetResult();
            if(!baseline.SameData(session.Current!))throw new IOException("La restauración de G5 no coincide.");checks.Add("G5 binding write/readback and full restoration");Save("binding-restored",true);
            Save("testing-profile2-report-rate",false);
            session.ApplyRateAsync(2,(rate+1)%4).GetAwaiter().GetResult();session.ApplyRateAsync(2,rate).GetAwaiter().GetResult();
            if(!baseline.SameData(session.Current!))throw new IOException("La restauración de frecuencia no coincide.");checks.Add("Report-rate write/readback and full restoration");
            var final=session.RefreshAsync().GetAwaiter().GetResult();if(!baseline.SameData(final))throw new IOException("La comparación final difiere del respaldo.");
            checks.Add("All profiles, all bindings, all macros, RGB and general block match baseline");Save("complete",true);return 0;
        }
        catch(Exception ex){Save("stopped-no-retry",false,ex.ToString());return 1;}
    }

    private static int Diagnose()
    {
        try
        {
            using var session = new KeyboardSession(NativeHid.Open(), Storage.Backup);
            var first = session.RefreshAsync().GetAwaiter().GetResult();
            var second = session.RefreshAsync().GetAwaiter().GetResult();
            if (!first.SameData(second)) throw new IOException("La configuración cambió entre lecturas.");
            var report = new { app = Updates.Current.ToString(), readOnly = true, independentlyReadSnapshots = 4, unchanged = true,
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
