using System.Buffers.Binary;
using K585Studio.Core;

internal static class LibraryChecks
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        using var model = new FakeTransport();
        var snapshot = new Snapshot(DateTimeOffset.UtcNow, model.Blocks[3], model.Blocks[5], model.Blocks[7], model.Blocks[9], model.Blocks[16]);
        var macro = new MacroDraft { Name = "Prueba", Steps = [new("Tecla ↓", "W", 0), new("Tecla ↑", "W", 150)] };
        var allocation = HardwareMacros.Allocate(snapshot, macro);
        var stored = snapshot with { Macros = allocation.Expected };
        var record = HardwareMacros.Read(stored.Macros).Single();
        var offline = MacroLibrary.Preview(macro, null);
        check(!offline.Ready && offline.Bytes == allocation.Data.Length, "Offline macro preview does not claim capacity");
        var fits = MacroLibrary.Preview(macro, snapshot);
        check(fits.Ready && !fits.ReusesRecord && fits.Bytes == allocation.Data.Length, "Capacity preview matches allocator");
        var reused = MacroLibrary.Preview(macro, stored);
        check(reused.Ready && reused.ReusesRecord, "Identical device record can be reused");
        var full = stored.Macros.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(full.AsSpan(2), 6144);
        check(MacroLibrary.Preview(macro, stored with { Macros = full }).ReusesRecord, "Full table still allows reuse");
        var different = new MacroDraft { Name = "Nueva", Steps = macro.Steps };
        check(!MacroLibrary.Preview(different, stored with { Macros = full }).Ready, "Full table rejects new record in preview");
        var packed = allocation.Expected.ToArray(); allocation.Data.CopyTo(packed, 18);
        BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(16), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(2), (ushort)(18 + allocation.Data.Length));
        check(!MacroLibrary.Preview(different, stored with { Macros = packed }).Ready, "Preview detects packed directory");
        var bindings = snapshot.Bindings.ToArray(); bindings[3] = 0x70; bindings[4] = 0; bindings[5] = 1;
        check(!MacroLibrary.Preview(macro, snapshot with { Bindings = bindings }).Ready, "Preview rejects activation of dangling P1 macro");
        var unbalanced = new MacroDraft { Name = "Incompleta", Steps = [new("Tecla ↓", "W", 0)] };
        check(!MacroLibrary.Preview(unbalanced, snapshot).Ready, "Unbalanced macro shown as not ready");
        var oem = new MacroDraft { Name = "OEM", Steps = [new("OEM", "3:0001", 0)] };
        check(!MacroLibrary.Preview(oem, snapshot).Ready, "OEM events remain visible but cannot be uploaded");
        check(MacroLibrary.DescribeMemory(snapshot).Contains("5872 bytes"), "Empty table memory reserves directory");
        check(MacroLibrary.DescribeMemory(stored).Contains($"{6144 - allocation.DataOffset - allocation.Data.Length} bytes"), "Device tail-space report exact");
        var before = stored.Digest;
        var project = new StudioDraft();
        var local = MacroLibrary.CopyTo(project, record);
        check(local.Id != record.Draft.Id && !ReferenceEquals(local.Steps, record.Draft.Steps), "Copy has independent ID and step collection");
        local.Name = "Editada"; local.Steps.Add(new("Pausa", "", 200));
        check(record.Draft.Name == "Prueba" && record.Draft.Steps.Count == 2 && stored.Digest == before, "Local edits never change device record or snapshot");
        var copy = MacroLibrary.CopyTo(project, record);
        check(copy.Id != local.Id && copy.Name == "Prueba", "Read-back source can be copied after local edits");
        check(ReferenceEquals(MacroLibrary.CopyTo(project, record), copy) && project.Macros.Count == 2, "Repeated copy does not multiply local entries");
        var collision = new StudioDraft { Macros = [new() { Name = "PRUEBA" }] };
        var renamed = MacroLibrary.CopyTo(collision, record);
        check(renamed.Name == "Prueba (2)" && collision.Macros[0].Name == "PRUEBA", "Same-name different-content import preserves local macro");
        var longName = new string('a', 100);
        var named = new StudioDraft { Macros = [new() { Name = longName }] };
        var bounded = MacroLibrary.CopyTo(named, record with { Draft = new() { Name = longName, Steps = macro.Steps } });
        check(bounded.Name.Length == 100 && bounded.Name.EndsWith(" (2)"), "Collision suffix respects local name limit");
        var badRecord = record with { Draft = new() { Name = "Bad", Steps = [new("Pausa", "", 60001)] } };
        reject(() => MacroLibrary.CopyTo(project, badRecord), "Invalid copied macro accepted");
        check(project.Macros.Count == 2, "Failed copy leaves library unchanged");
        check(model.Writes == 0 && stored.Digest == before, "Preview, inventory and copy never reach HID or change snapshot");

        string folder = Path.Combine(Path.GetTempPath(), "K585Studio-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); string file = Path.Combine(folder, "test.k585.json");
        try
        {
            foreach (string json in new[] { "null", "{", "{\"Assignments\":null}", "{\"Macros\":[null]}", "{\"Macros\":[{\"Name\":null}]}",
                "{\"Macros\":[{\"Steps\":null}]}", "{\"Macros\":[{\"Steps\":[null]}]}", "{\"Assignments\":{\"2\":null}}",
                "{\"Assignments\":{\"2\":{\"W\":null}}}", "{\"Lighting\":{\"2\":null}}", "{\"ReportRates\":null}",
                "{\"Assignments\":{\"2\":{\"W\":{\"Category\":\"Unknown\",\"Value\":\"X\"}}}}",
                "{\"Assignments\":{\"2\":{\"W\":{\"Category\":\"Macro\",\"Value\":\"Missing\"}}}}",
                "{\"Assignments\":{\"2\":{\"W\":{\"Category\":\"Original\",\"Value\":\"Q\"}}}}" })
            {
                File.WriteAllText(file, json);
                bool rejected = false;
                try { StudioDraft.Load(file); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Malformed project rejected with readable validation: " + json);
            }
            File.WriteAllText(file, new string(' ', StudioDraft.MaximumFileBytes + 1));
            reject(() => StudioDraft.Load(file), "Oversized project accepted");
            project.Assign(2, "W", new("Macro", copy.Name)); project.Assign(3, "Q", new("Original", "Q"));
            project.Save(file); var roundtrip = StudioDraft.Load(file);
            check(roundtrip.Macros.Count == 2 && roundtrip.Profile(2)["W"].Value == copy.Name, "Existing schema 1 projects roundtrip with pending macros");
            byte[] saved = File.ReadAllBytes(file);
            project.Assignments[0] = [];
            reject(() => project.Save(file), "Invalid-profile project saved");
            check(File.ReadAllBytes(file).AsSpan().SequenceEqual(saved), "Failed save preserves prior project byte-for-byte");
            reject(() => new StudioDraft().Assign(2, "Fn", new("Teclado", "W")), "Reserved Fn draft accepted");
        }
        finally { File.Delete(file); Directory.Delete(folder); }
    }
}
