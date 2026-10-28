using System.Text.Json;

namespace K585Studio.Core;

public sealed record Assignment(string Category, string Value);
public sealed record MacroStep(string Action, string Value, int DelayMs);
public sealed class MacroDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nueva macro";
    public int Cycles { get; set; } = 1;
    public List<MacroStep> Steps { get; set; } = [];
    public override string ToString() => Name;
}
public sealed class StudioDraft
{
    public int Schema { get; set; } = 1;
    public Dictionary<int, Dictionary<string, Assignment>> Assignments { get; set; } = [];
    public List<MacroDraft> Macros { get; set; } = [];
    public bool RainFirmwareConfirmed { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;
    public Dictionary<int, Lighting> Lighting { get; set; } = [];
    public Dictionary<int, int> ReportRates { get; set; } = [];

    public IReadOnlyDictionary<string, Assignment> Profile(int profile) =>
        profile != 1 && Assignments.TryGetValue(profile, out var keys) ? keys : new Dictionary<string, Assignment>();
    public void Assign(int profile, string key, Assignment? assignment)
    {
        if (profile is < 2 or > 4) throw new InvalidOperationException("El perfil 1 está protegido.");
        if (assignment == null) { if (Assignments.TryGetValue(profile, out var old)) old.Remove(key); return; }
        if (!Assignments.TryGetValue(profile, out var map)) Assignments[profile] = map = [];
        map[key] = assignment;
    }
    public void Validate()
    {
        if (Schema != 1 || Assignments.Keys.Any(p => p is < 2 or > 4) || Lighting.Keys.Any(p => p is < 2 or > 4) ||
            ReportRates.Any(p => p.Key is < 2 or > 4 || p.Value is < 0 or > 3) || Macros.Count > 100 ||
            Macros.Any(m => m.Name.Length is < 1 or > 100 || m.Cycles is < 1 or > 999 || m.Steps.Count > 2000 ||
                m.Steps.Any(s => s.DelayMs is < 0 or > 60000 || s.Value.Length > 200)))
            throw new InvalidDataException("El proyecto contiene datos no admitidos o intenta modificar el perfil 1.");
        foreach (var light in Lighting.Values) light.Validate();
        foreach (var map in Assignments.Values)
            foreach (var (key, value) in map)
                if (BindingCatalog.Key(key).Reserved || value.Value.Length > 200 || value.Category.Length > 50)
                    throw new InvalidDataException("El proyecto contiene una tecla reservada o asignación no válida.");
        if (Macros.Select(m => m.Id).Distinct().Count() != Macros.Count ||
            Macros.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Macros.Count)
            throw new InvalidDataException("Hay macros con nombres o identificadores repetidos.");
    }
    public static StudioDraft Load(string path)
    {
        var draft = JsonSerializer.Deserialize<StudioDraft>(File.ReadAllText(path)) ?? throw new InvalidDataException("Proyecto vacío.");
        draft.Validate(); return draft;
    }
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
