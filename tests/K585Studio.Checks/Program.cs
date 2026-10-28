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
foreach (var p in new[] { -1, 0, 1, 3, 4, 5 }) Reject(() => Protocol.LightingRequest(p, Lighting.Default), "Protected profile write accepted");
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
    await RejectAsync(() => session.ApplyLightingAsync(1, Lighting.Default, true), "Profile 1 accepted");
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
Reject(() => draft.Assign(1, "W", new("Teclado", "X")), "Local profile 1 edit accepted");
draft.Assign(2, "W", new("Teclado", "X")); Check(draft.Profile(2)["W"].Value == "X" && draft.Profile(1).Count == 0, "Draft profile scope");
draft.Assign(2, "W", null); Check(draft.Profile(2).Count == 0, "Draft revert");
draft.Assignments[1] = []; Reject(draft.Validate, "Imported profile 1 accepted"); draft.Assignments.Remove(1);
draft.Macros.Add(new() { Name = "A", Steps = [new("Tecla ↓", "W", -1)] }); Reject(draft.Validate, "Invalid macro delay accepted"); draft.Macros.Clear();
draft.Macros.Add(new() { Name = "A" }); draft.Macros.Add(new() { Name = "a" }); Reject(draft.Validate, "Duplicate macro names accepted");

string Release(string tag, string url = "https://github.com/Esteban-3055/K585Studio/releases/tag/test") =>
    System.Text.Json.JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = tag, html_url = url });
Check(Updates.Parse(Release("v0.1")).Release == null, "v0.1 compared incorrectly with v0.1.0");
Check(Updates.Parse(Release("v0.2.0")).Release != null, "Newer release missing");
Reject(() => Updates.Parse(Release("v0.2", "https://evil.example/file.exe")), "Untrusted update URL accepted");
Reject(() => Updates.Parse(Release("v0.2", "https://github.com/another/repo/releases/tag/v0.2")), "Wrong repo accepted");
Reject(() => Updates.Parse(Release("bad")), "Invalid version accepted");
Console.WriteLine($"PASS: {checks} checks (protocol, protected profile, persistence gate, readback faults, local drafts, releases). No physical device used.");

sealed class FakeTransport : ITransport
{
    public Dictionary<byte, byte[]> Blocks { get; } = Protocol.Regions.ToDictionary(x => x.Key, x => Enumerable.Range(0, x.Value).Select(i => (byte)(i % 251)).ToArray());
    public int Writes { get; private set; }
    public bool CorruptOnWrite { get; init; }
    public bool TimeoutOnWrite { get; init; }
    public bool Unstable { get; init; }
    public FakeTransport() { Blocks[3][10] = 1; Lighting.Default.Bytes().CopyTo(Blocks[5], 64); }
    public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(5)), count = request[4];
        var response = request.ToArray();
        if (request[3] == 6)
        {
            if (offset != 64 || count != 8) throw new Exception("Unsafe write packet reached transport");
            Writes++; request.AsSpan(8, count).CopyTo(Blocks[5].AsSpan(offset));
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
