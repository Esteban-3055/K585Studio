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
    public const int MaximumFileBytes = 2 * 1024 * 1024;
    public int Schema { get; set; } = 1;
    public Dictionary<int, Dictionary<string, Assignment>> Assignments { get; set; } = [];
    public List<MacroDraft> Macros { get; set; } = [];
    public bool RainFirmwareConfirmed { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;
    public Dictionary<int, Lighting> Lighting { get; set; } = [];
    public Dictionary<int, int> ReportRates { get; set; } = [];

    public IReadOnlyDictionary<string, Assignment> Profile(int profile) =>
        Assignments.TryGetValue(profile, out var keys) ? keys : new Dictionary<string, Assignment>();
    public void Assign(int profile, string key, Assignment? assignment)
    {
        Protocol.RequireEditableProfile(profile);
        if (BindingCatalog.Key(key).Reserved) throw new InvalidOperationException("Fn es una función reservada.");
        if (assignment == null) { if (Assignments.TryGetValue(profile, out var old)) old.Remove(key); return; }
        if (!Assignments.TryGetValue(profile, out var map)) Assignments[profile] = map = [];
        map[key] = assignment;
    }
    public void Validate()
    {
        if (Assignments is null || Macros is null || Lighting is null || ReportRates is null ||
            Assignments.Values.Any(map => map is null || map.Any(p => p.Key is null || p.Value is null || p.Value.Category is null || p.Value.Value is null)) ||
            Lighting.Values.Any(light => light is null) || Macros.Any(m => m is null || m.Name is null || m.Steps is null ||
                m.Steps.Any(s => s is null || s.Action is null || s.Value is null)))
            throw new InvalidDataException("El proyecto contiene campos vacíos o incompletos.");
        if (Schema != 1 || Assignments.Keys.Any(p => p is < 1 or > 4) || Lighting.Keys.Any(p => p is < 1 or > 4) ||
            ReportRates.Any(p => p.Key is < 1 or > 4 || p.Value is < 0 or > 3) || Macros.Count > 100 ||
            Macros.Any(m => string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 100 || m.Id == Guid.Empty || m.Cycles is < 1 or > 999 || m.Steps.Count > 2000 ||
                m.Steps.Any(s => s.DelayMs is < 0 or > 60000 || s.Value.Length > 200 ||
                    s.Action is not ("Tecla ↓" or "Tecla ↑" or "Ratón ↓" or "Ratón ↑" or "Rueda" or "Pausa" or "OEM"))))
            throw new InvalidDataException("El proyecto contiene datos o números de perfil no admitidos.");
        foreach (var light in Lighting.Values) light.Validate();
        foreach (var map in Assignments.Values)
            foreach (var (key, value) in map)
            {
                if (BindingCatalog.Key(key).Reserved || value.Value.Length > 200 || value.Category.Length > 50)
                    throw new InvalidDataException("El proyecto contiene una tecla reservada o asignación no válida.");
                if (value.Category == "Macro")
                {
                    if (!Macros.Any(m => m.Name == value.Value)) throw new InvalidDataException("Una asignación referencia una macro que no está en la biblioteca.");
                }
                else if (value.Category == "Original")
                {
                    if (value.Value != key) throw new InvalidDataException("La asignación original no corresponde a esa tecla.");
                }
                else if (!(value.Category == "Ratón" && value.Value == "Doble clic")) BindingCatalog.Encode(value).Validate();
            }
        if (Macros.Select(m => m.Id).Distinct().Count() != Macros.Count ||
            Macros.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Macros.Count)
            throw new InvalidDataException("Hay macros con nombres o identificadores repetidos.");
    }
    public static StudioDraft Load(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumFileBytes) throw new InvalidDataException("El proyecto supera el límite de 2 MiB.");
        try
        {
            var draft = JsonSerializer.Deserialize<StudioDraft>(input) ?? throw new InvalidDataException("Proyecto vacío.");
            draft.Validate(); return draft;
        }
        catch (JsonException ex) { throw new InvalidDataException("El archivo no contiene un proyecto JSON válido.", ex); }
    }
    public void Save(string path)
    {
        Validate();
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions { WriteIndented = true });
        if (content.Length > MaximumFileBytes) throw new InvalidDataException("El proyecto supera el límite de 2 MiB. Reduce la biblioteca antes de guardar.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, content);
        File.Move(temporary, path, true);
    }
}
