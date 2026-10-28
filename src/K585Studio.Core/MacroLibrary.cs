using System.Buffers.Binary;

namespace K585Studio.Core;

public sealed record MacroReadiness(bool Ready, bool ReusesRecord, int Bytes, string Message);

public static class MacroLibrary
{
    // Reading the device must never overwrite, rename or silently import local work.
    public static MacroDraft CopyTo(StudioDraft project, DeviceMacro source)
    {
        var existing = project.Macros.FirstOrDefault(m => m.Cycles == source.Draft.Cycles &&
            m.Steps.SequenceEqual(source.Draft.Steps));
        if (existing != null) return existing;
        if (project.Macros.Count >= 100) throw new InvalidDataException("La biblioteca local admite hasta 100 macros.");
        string root = source.Draft.Name.Trim();
        if (root.Length == 0) root = $"Macro del teclado {source.Index + 1}";
        root = root[..Math.Min(root.Length, 100)];
        string name = root;
        for (int suffix = 2; project.Macros.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); suffix++)
        {
            string end = $" ({suffix})";
            name = root[..Math.Min(root.Length, 100 - end.Length)] + end;
        }
        var copy = new MacroDraft { Name = name, Cycles = source.Draft.Cycles, Steps = [..source.Draft.Steps] };
        project.Macros.Add(copy);
        try { project.Validate(); }
        catch { project.Macros.Remove(copy); throw; }
        return copy;
    }

    public static MacroReadiness Preview(MacroDraft macro, Snapshot? snapshot)
    {
        int bytes;
        try { bytes = HardwareMacros.Encode(macro).Length; }
        catch (InvalidDataException ex) { return new(false, false, 0, ex.Message); }
        if (snapshot == null) return new(false, false, bytes, $"{bytes} bytes · lee el teclado para comprobar el espacio disponible.");
        try
        {
            var encoded = HardwareMacros.Encode(macro);
            var existing = HardwareMacros.Read(snapshot.Macros).FirstOrDefault(m =>
                snapshot.Macros.AsSpan(m.Offset, m.Length).SequenceEqual(encoded));
            if (existing != null) return new(true, true, bytes, $"Ya existe en el teclado (#{existing.Index + 1}). Se reutiliza sin ocupar más memoria.");
            HardwareMacros.Allocate(snapshot, macro);
            return new(true, false, bytes, $"Lista para asignar · {bytes} bytes nuevos. La aplicación vuelve a comprobarlo al aplicar.");
        }
        catch (InvalidDataException ex) { return new(false, false, bytes, ex.Message); }
    }

    public static string DescribeMemory(Snapshot? snapshot)
    {
        if (snapshot == null) return "Lee el teclado para ver su biblioteca y memoria.";
        try
        {
            var records = HardwareMacros.Read(snapshot.Macros);
            int used = BinaryPrimitives.ReadUInt16LittleEndian(snapshot.Macros.AsSpan(2));
            int start = records.Count == 0 ? 16 + HardwareMacros.MaximumCount * 2 : used;
            return $"Teclado: {records.Count}/{HardwareMacros.MaximumCount} macros · {HardwareMacros.Capacity - start} bytes al final. Se comprueba también el directorio al asignar.";
        }
        catch (InvalidDataException ex) { return "Tabla del teclado conservada: " + ex.Message; }
    }
}
