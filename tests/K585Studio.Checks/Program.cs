using System.Buffers.Binary;
using K585Studio.Core;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Reject(Action action, string label) { try { action(); } catch { checks++; return; } throw new Exception(label); }
async Task RejectAsync(Func<Task> action, string label) { try { await action(); } catch { checks++; return; } throw new Exception(label); }

foreach (var (op, length) in Protocol.Regions)
{
    var request = Protocol.ReadRequest(op, length - 1, 1);
    Check(request.Length == 64 && request[0] == 4, "Report size/id");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(1)) == request.Skip(3).Sum(b => b), "Request checksum");
    Reject(() => Protocol.ReadRequest(op, length, 1), "Read region overflow accepted");
    Reject(() => Protocol.ReadRequest(op, -1, 1), "Negative offset accepted");
}
foreach (var op in new byte[] { 4, 6, 8, 10, 17, 255 }) Reject(() => Protocol.ReadRequest(op, 0, 1), "Write opcode accepted as read");
foreach (var p in new[] { -1, 0, 5 }) Reject(() => Protocol.LightingRequest(p, Lighting.Default), "Invalid profile write accepted");
Reject(() => Protocol.LightingRequest(2, Lighting.Default with { Mode = 20 }), "Removed effect 20 accepted");
Reject(() => Protocol.LightingRequest(2, Lighting.Default with { Brightness = 5 }), "Brightness out of range");
Reject(() => Protocol.LightingRequest(2, Lighting.Default with { Speed = 5 }), "Speed out of range");
Reject(() => Protocol.LightingRequest(2, Lighting.Default with { Direction = 42 }), "Unknown direction accepted");
var req = Protocol.ReadRequest(5, 64, 8); var reply = req.ToArray(); reply[8] = 23; reply[9] = 4;
Check(Protocol.Response(req, reply)[0] == 23, "Request checksum echo must allow different response payload");
foreach (int index in new[] { 0, 1, 2, 3, 4, 5, 6 })
{ var bad = reply.ToArray(); bad[index] ^= 1; Reject(() => Protocol.Response(req, bad), "Mismatched response accepted"); }
foreach (byte status in new byte[] { 0xfe, 0xff }) { var bad = reply.ToArray(); bad[7] = status; Reject(() => Protocol.Response(req, bad), "Rejected status accepted"); }

var fake = new FakeTransport(); int backups = 0;
using (var session = new KeyboardSession(fake, _ => { backups++; return Task.CompletedTask; }))
{
    var before = await session.RefreshAsync(); Check(backups == 1 && fake.Writes == 0, "Refresh wrote to hardware");
    await RejectAsync(() => session.ApplyLightingAsync(0, Lighting.Default, true), "Invalid profile accepted");
    await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default, false), "Unconfirmed firmware accepted");
    Check(fake.Writes == 0, "Rejected writes reached transport");
    var expected = before.Profile(2) with { R = 120, Colorful = true, Speed = 3 };
    var after = await session.ApplyLightingAsync(2, expected, true);
    Check(fake.Writes == 1 && backups == 3 && before.OnlyProfile2LightingChanged(after, expected), "Write scope/readback");
    Check(!session.WriteFaulted, "Successful verification stayed faulted");
    await session.ApplyLightingAsync(2, expected, true); Check(fake.Writes == 1, "No-op caused another persistent write");
    fake.Blocks[7][200] ^= 1;
    await RejectAsync(() => session.ApplyLightingAsync(2, expected with { R = 1 }, true), "Stale snapshot write accepted");
    Check(fake.Writes == 1, "Stale read reached persistent write");
}
var inactive = new FakeTransport(); inactive.Blocks[3][10] = 0;
using (var session = new KeyboardSession(inactive, _ => Task.CompletedTask))
{ await session.RefreshAsync(); await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default with { R = 1 }, true), "Inactive P2 accepted"); Check(inactive.Writes == 0, "Activated profile remotely"); }
var corrupt = new FakeTransport { CorruptOnWrite = true };
using (var session = new KeyboardSession(corrupt, _ => Task.CompletedTask))
{
    await session.RefreshAsync();
    await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default with { R = 1 }, true), "Profile 1 corruption undetected");
    Check(session.WriteFaulted, "Readback failure did not lock writes");
    await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default, true), "Second write after fault accepted");
    Check(corrupt.Writes == 1, "Retry after fault");
}
var timeout = new FakeTransport { TimeoutOnWrite = true };
using (var session = new KeyboardSession(timeout, _ => Task.CompletedTask))
{
    await session.RefreshAsync(); await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default with { R = 1 }, true), "Timeout ignored");
    Check(session.WriteFaulted && timeout.Writes == 1, "Timeout retried or unlocked");
}
var backupFailure = new FakeTransport(); bool failBackup = false;
using (var session = new KeyboardSession(backupFailure, _ => failBackup ? Task.FromException(new IOException("disk full")) : Task.CompletedTask))
{
    await session.RefreshAsync(); failBackup = true;
    await RejectAsync(() => session.ApplyLightingAsync(2, Lighting.Default with { R = 1 }, true), "Backup failure ignored");
    Check(backupFailure.Writes == 0, "Wrote without backup");
}
var unstable = new FakeTransport { Unstable = true };
using (var session = new KeyboardSession(unstable, _ => Task.CompletedTask))
{ await RejectAsync(() => session.RefreshAsync(), "Unstable double read accepted"); Check(unstable.Writes == 0, "Refresh writes"); }

var draft = new StudioDraft();
draft.Assign(1,"W",new("Teclado","Q"));Check(draft.Profile(1)["W"].Value=="Q","Profile 1 draft editable");
draft.Assign(2, "W", new("Teclado", "X")); Check(draft.Profile(2)["W"].Value == "X" && draft.Profile(1)["W"].Value == "Q", "Draft profile scope");
draft.Assign(2, "W", null); Check(draft.Profile(2).Count == 0, "Draft revert");
draft.Assignments[0] = []; Reject(draft.Validate, "Imported invalid profile accepted"); draft.Assignments.Remove(0);
draft.Macros.Add(new() { Name = "A", Steps = [new("Tecla ↓", "W", -1)] }); Reject(draft.Validate, "Invalid macro delay accepted"); draft.Macros.Clear();
draft.Macros.Add(new() { Name = "A" }); draft.Macros.Add(new() { Name = "a" }); Reject(draft.Validate, "Duplicate macro names accepted");

string Release(string tag, string? url = null) =>
    System.Text.Json.JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = tag, html_url = url ?? "https://github.com/Esteban-3055/K585Studio/releases/tag/"+tag, assets = new[]{new{name="K585Studio-0.4.0-Setup-win-x64.exe",size=4,digest="sha256:"+new string('a',64),state="uploaded",browser_download_url="https://github.com/Esteban-3055/K585Studio/releases/download/v0.4.0/K585Studio-0.4.0-Setup-win-x64.exe"}} });
Check(Updates.Parse(Release("v0.1")).Release == null, "v0.1 compared incorrectly with v0.1.0");
Check(Updates.Parse(Release("v0.4.0")).Installer != null, "Newer installer missing");
Reject(() => Updates.Parse(Release("v0.2", "https://evil.example/file.exe")), "Untrusted update URL accepted");
Reject(() => Updates.Parse(Release("v0.2", "https://github.com/another/repo/releases/tag/v0.2")), "Wrong repo accepted");
Reject(() => Updates.Parse(Release("bad")), "Invalid version accepted");
foreach(int p in new[]{1,2,3,4})
{
    var packet=Protocol.LightingRequest(p,Lighting.Default);Check(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(5))==(p-1)*64,"Lighting profile scope");
    foreach(var key in BindingCatalog.Keys.Where(k=>!k.Reserved))
    {
        var write=Protocol.BindingRequest(p,key.Name,KeyBinding.Keyboard(27));
        Check(write[3]==8&&write[4]==3&&BinaryPrimitives.ReadUInt16LittleEndian(write.AsSpan(5))==(p-1)*192+key.Slot*3,"Binding packet offset");
    }
    for(int rate=0;rate<4;rate++)Check(BinaryPrimitives.ReadUInt16LittleEndian(Protocol.RateRequest(p,rate).AsSpan(5))==(p-1)*64+15,"Per-profile report rate");
}
Reject(()=>Protocol.BindingRequest(0,"W",KeyBinding.Keyboard(27)),"Invalid binding profile allowed");Reject(()=>Protocol.RateRequest(5,0),"Invalid rate profile allowed");
Reject(()=>Protocol.BindingRequest(2,"Fn",KeyBinding.Keyboard(27)),"Reserved Fn edit allowed");Reject(()=>Protocol.RateRequest(2,4),"Invalid rate allowed");
Check(BindingCatalog.Keys.Count(k=>!k.Reserved)==41,"Physical key count");
Check(BindingCatalog.ParseKeyboard("Ctrl + Shift + W")==new KeyBinding(0xb0,3,26),"Combination encoding");
Check(BindingCatalog.ParseKeyboard("Ctrl")==new KeyBinding(2,1,1),"Standalone modifier encoding");
foreach(var key in BindingCatalog.Keyboard)Check(BindingCatalog.ParseKeyboard(key.Key)==KeyBinding.Keyboard(key.Value),"Keyboard catalogue roundtrip: "+key.Key);
Check(BindingCatalog.ParseKeyboard("Ctrl + Num +")==KeyBinding.Keyboard(87,1),"Keypad plus combination");
foreach(var binding in BindingCatalog.Media.Values.Concat(BindingCatalog.Mouse.Values).Concat(BindingCatalog.Functions.Values)) { binding.Validate(); checks++; }
Reject(()=>new KeyBinding(5,2,2).Validate(),"Reserved Fn output allowed");
var macroDraft=new MacroDraft{Name="Studio test",Cycles=2,Steps=[new("Tecla ↓","W",0),new("Tecla ↑","W",150)]};
var model=new FakeTransport();var beforeModel=new Snapshot(DateTimeOffset.UtcNow,model.Blocks[3],model.Blocks[5],model.Blocks[7],model.Blocks[9],model.Blocks[16]);
var allocation=HardwareMacros.Allocate(beforeModel,macroDraft);var records=HardwareMacros.Read(allocation.Expected);
Check(records.Count==1&&records[0].Draft.Name==macroDraft.Name&&records[0].Draft.Cycles==2&&records[0].Draft.Steps.SequenceEqual(macroDraft.Steps),"Macro encode/decode timing roundtrip");
Check(allocation.DataOffset==272,"Reserved macro directory space");
Reject(()=>HardwareMacros.Encode(new(){Name="Unbalanced",Steps=[new("Tecla ↓","W",0)]}),"Held key macro allowed");
Reject(()=>HardwareMacros.Encode(new MacroDraft{Name="Cycles",Cycles=256,Steps=macroDraft.Steps}),"Unsupported repeat count allowed");
var nextAllocation=HardwareMacros.Allocate(beforeModel with{Macros=allocation.Expected},new(){Name="Second macro",Steps=macroDraft.Steps});
Check(nextAllocation.Expected.AsSpan(allocation.DataOffset,allocation.Data.Length).SequenceEqual(allocation.Data),"Appending macro preserves existing record byte for byte");
Check(HardwareMacros.Read(nextAllocation.Expected).Count==2,"Second macro directory entry published");
var dangling=beforeModel.Bindings.ToArray();dangling[3]=0x70;dangling[4]=0;dangling[5]=1;
Reject(()=>HardwareMacros.Allocate(beforeModel with{Bindings=dangling},macroDraft),"Allocation activates dangling profile-1 macro reference");
var packed=allocation.Expected.ToArray();allocation.Data.CopyTo(packed,18);BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(16),18);BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(2),(ushort)(18+allocation.Data.Length));
Reject(()=>HardwareMacros.Allocate(beforeModel with{Macros=packed},macroDraft),"Packed OEM macros relocated");
var full=allocation.Expected.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(full.AsSpan(2),6144);
Reject(()=>HardwareMacros.Allocate(beforeModel with{Macros=full},macroDraft),"Macro capacity exceeded");
Reject(()=>HardwareMacros.Read(new byte[6144]),"Unknown macro header accepted");
var unsafeProject=new StudioDraft();unsafeProject.ReportRates[0]=3;Reject(unsafeProject.Validate,"Invalid imported report rate profile");
unsafeProject.ReportRates.Clear();unsafeProject.Lighting[5]=Lighting.Default;Reject(unsafeProject.Validate,"Invalid imported illumination profile");
using(var modelSession=new KeyboardSession(model,_=>Task.CompletedTask))
{
    var before=await modelSession.RefreshAsync();
    var changed=await modelSession.ApplyBindingAsync(2,"W",BindingCatalog.ParseKeyboard("Ctrl + X"));
    Check(before.Bindings.AsSpan(0,192).SequenceEqual(changed.Bindings.AsSpan(0,192)),"P1 bindings retained after remap");
    Check(BindingCatalog.Read(changed,2,"W")==new KeyBinding(0xb0,1,27),"Binding readback");
    var rate=await modelSession.ApplyRateAsync(2,3);Check(rate.Illumination[79]==3&&rate.Illumination.AsSpan(0,64).SequenceEqual(before.Illumination.AsSpan(0,64)),"Rate write isolated from P1");
    int start=model.Writes;var macroSnapshot=await modelSession.ApplyMacroAsync(2,"W",macroDraft);
    Check(HardwareMacros.Read(macroSnapshot.Macros).Count==1&&BindingCatalog.Read(macroSnapshot,2,"W")==KeyBinding.Macro(0),"Macro allocated then bound");
    Check(model.Commands.Skip(start).Last()==8,"Binding published last");
    int afterCount=model.Writes;await modelSession.ApplyMacroAsync(2,"E",macroDraft);Check(model.Writes==afterCount+1,"Identical macro reuses record without rewriting table");
    Check(before.Bindings.AsSpan(0,192).SequenceEqual(modelSession.Current!.Bindings.AsSpan(0,192)),"P1 retained across macro allocation");
}
var fixtureFolder=args.Length==2&&args[0]=="--fixtures"?args[1]:null;
if(fixtureFolder!=null)
{
    Directory.CreateDirectory(fixtureFolder);File.WriteAllBytes(Path.Combine(fixtureFolder,"macro-table.bin"),allocation.Expected);File.WriteAllText(Path.Combine(fixtureFolder,"key-catalog.json"),System.Text.Json.JsonSerializer.Serialize(BindingCatalog.Keys));
    var packets=new List<byte[]>();foreach(int p in Enumerable.Range(1,4))
    {
        packets.Add(Protocol.ProfileRequest(p));packets.Add(Protocol.LightingRequest(p,Lighting.Default));
        foreach(var key in BindingCatalog.Keys.Where(k=>!k.Reserved))packets.Add(Protocol.BindingRequest(p,key.Name,KeyBinding.Keyboard(27)));
        for(int rate=0;rate<4;rate++)packets.Add(Protocol.RateRequest(p,rate));
    }
    File.WriteAllText(Path.Combine(fixtureFolder,"profile-packets.json"),System.Text.Json.JsonSerializer.Serialize(packets));
}
await UpdateChecks.Run(Check,RejectAsync);
LibraryChecks.Run(Check,Reject);
await ProfileChecks.Run(Check,Reject,RejectAsync);
Console.WriteLine($"PASS: {checks} checks (protocol, profile selection, isolated writes, persistence, bindings, macros, report rates and releases). No physical device used.");

sealed class FakeTransport : ITransport
{
    public Dictionary<byte, byte[]> Blocks { get; } = Protocol.Regions.ToDictionary(x => x.Key, x => Enumerable.Range(0, x.Value).Select(i => (byte)(i % 251)).ToArray());
    public int Writes { get; private set; }
    public List<byte> Commands { get; } = [];
    public bool CorruptOnWrite { get; init; }
    public bool TimeoutOnWrite { get; init; }
    public bool Unstable { get; init; }
    public FakeTransport() { Blocks[3][10] = 1; Lighting.Default.Bytes().CopyTo(Blocks[5], 64); Array.Clear(Blocks[9]);Blocks[9][0]=0xaa;Blocks[9][1]=0x55;Blocks[9][2]=16;Blocks[9][6]=1; }
    public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(5)), count = request[4];
        var response = request.ToArray();
        if (request[3] is 4 or 6 or 8 or 10)
        {
            if(request[3]==4&&(offset!=10||count!=1||request[8]>3))throw new Exception("Invalid active-profile write");
            if(request[3]==6&&(offset<0||offset+count>256))throw new Exception("Lighting write bounds");
            if(request[3]==8&&(offset<0||offset+count>768))throw new Exception("Binding write bounds");
            Writes++;Commands.Add(request[3]);request.AsSpan(8,count).CopyTo(Blocks[request[3] switch{4=>3,6=>5,8=>7,_=>9}].AsSpan(offset));
            if (CorruptOnWrite) Blocks[7][0] ^= 1;
            if (TimeoutOnWrite) throw new TimeoutException("write may have succeeded");
        }
        else
        {
            if (Unstable && request[3] == 3 && offset == 0) Blocks[3][0]++;
            Blocks[request[3]].AsSpan(offset, count).CopyTo(response.AsSpan(8));
        }
        return Task.FromResult(response);
    }
    public void Dispose() { }
}
