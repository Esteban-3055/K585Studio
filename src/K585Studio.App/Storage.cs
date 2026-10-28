using System.Security.Cryptography;
using System.Text.Json;
using K585Studio.Core;

namespace K585Studio.App;

internal static class Storage
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "K585Studio");
    public static string DraftFile => Path.Combine(Root, "project.json");
    public static Task Backup(Snapshot snapshot)
    {
        var folder = Path.Combine(Root, "Backups", $"{snapshot.ReadAt:yyyyMMdd-HHmmss-fffffff}-{snapshot.Digest[..12]}");
        Directory.CreateDirectory(folder);
        var blocks = new Dictionary<string, byte[]> { ["general"] = snapshot.General, ["illumination"] = snapshot.Illumination,
            ["bindings"] = snapshot.Bindings, ["macros"] = snapshot.Macros, ["rgb"] = snapshot.Rgb };
        foreach (var (name, bytes) in blocks) File.WriteAllBytes(Path.Combine(folder, name + ".bin"), bytes);
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new
        {
            schema = 1, app = "0.1.0", device = "0C45:5004 / FF1C:0092", snapshot.ReadAt,
            snapshot.ActiveProfile, snapshot.Digest, sha256 = blocks.ToDictionary(x => x.Key, x => Convert.ToHexString(SHA256.HashData(x.Value)))
        }, new JsonSerializerOptions { WriteIndented = true }));
        return Task.CompletedTask;
    }
}
