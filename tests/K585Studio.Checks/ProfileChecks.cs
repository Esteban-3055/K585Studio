using System.Buffers.Binary;
using K585Studio.Core;

internal static class ProfileChecks
{
    internal static async Task Run(Action<bool,string> check,Action<Action,string> reject,Func<Func<Task>,string,Task> rejectAsync)
    {
        foreach(int invalid in new[]{-1,0,5,256})reject(()=>Protocol.ProfileRequest(invalid),"Invalid profile selection accepted");
        foreach(int p in Enumerable.Range(1,4))
        {
            var request=Protocol.ProfileRequest(p);
            check(request[3]==4&&request[4]==1&&BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(5))==10&&request[8]==p-1,
                "Profile selection changes only general byte 10");
            check(request.Skip(9).All(b=>b==0)&&BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(1))==request.Skip(3).Sum(b=>b),"Profile packet checksum and padding");
            var rejected=request.ToArray();rejected[7]=1;reject(()=>Protocol.Response(request,rejected),"Nonzero profile-write status accepted");
        }
        using var model=new FakeTransport();int backups=0;
        using var session=new KeyboardSession(model,_=>{backups++;return Task.CompletedTask;});
        var baseline=await session.RefreshAsync();
        foreach(int p in Enumerable.Range(1,4))
        {
            var before=session.Current!;var selected=await session.SelectProfileAsync(p);
            var general=before.General.ToArray();general[10]=(byte)(p-1);
            check(selected.ActiveProfile==p&&(before with{General=general}).SameData(selected),"Selection preserves every stored profile, binding and macro");
            int writes=model.Writes;await session.SelectProfileAsync(p);
            check(model.Writes==writes,"Selecting the already-active profile does not write");

            before=session.Current!;var lighting=Lighting.Default with{Mode=1,R=(byte)(30+p)};
            var after=await session.ApplyLightingAsync(p,lighting,false);
            var expected=before.Illumination.ToArray();lighting.Bytes().CopyTo(expected,(p-1)*64);
            check((before with{Illumination=expected}).SameData(after),"Lighting isolated to selected profile including P1");

            before=after;after=await session.ApplyBindingAsync(p,"W",BindingCatalog.ParseKeyboard("Ctrl + X"));
            var bindings=before.Bindings.ToArray();new byte[]{0xb0,1,27}.CopyTo(bindings,(p-1)*192+BindingCatalog.Key("W").Slot*3);
            check((before with{Bindings=bindings}).SameData(after),"Binding isolated to selected profile including P1");

            before=after;after=await session.ApplyRateAsync(p,3);
            expected=before.Illumination.ToArray();expected[(p-1)*64+15]=3;
            check((before with{Illumination=expected}).SameData(after),"Rate isolated to selected profile including P1");

            var macro=new MacroDraft{Name="Profile "+p,Steps=[new("Tecla ↓","E",0),new("Tecla ↑","E",120)]};
            before=after;after=await session.ApplyMacroAsync(p,"G5",macro);
            bindings=before.Bindings.ToArray();KeyBinding.Macro((byte)(p-1)).Bytes().CopyTo(bindings,(p-1)*192+BindingCatalog.Key("G5").Slot*3);
            check((before with{Bindings=bindings,Macros=after.Macros}).SameData(after),"Macro binding does not change other profiles");
            foreach(var record in HardwareMacros.Read(before.Macros))
                check(before.Macros.AsSpan(record.Offset,record.Length).SequenceEqual(after.Macros.AsSpan(record.Offset,record.Length)),"Append preserves previous macro records");
        }
        check(backups>0&&!session.WriteFaulted,"Every successful change retains verified session and backups");
        check(baseline.General[10]==1,"Original snapshot remains immutable after all operations");

        using(var corrupted=new FakeTransport{CorruptOnWrite=true})
        using(var badSession=new KeyboardSession(corrupted,_=>Task.CompletedTask))
        {
            await badSession.RefreshAsync();await rejectAsync(()=>badSession.SelectProfileAsync(1),"Selection ignored collateral corruption");
            check(badSession.WriteFaulted,"Unverified selection blocks writes");
            await rejectAsync(()=>badSession.SelectProfileAsync(4),"Selection retried after uncertain write");
            check(corrupted.Writes==1,"Uncertain profile write is not retried");
        }
        using(var timed=new FakeTransport{TimeoutOnWrite=true})
        using(var timedSession=new KeyboardSession(timed,_=>Task.CompletedTask))
        {
            await timedSession.RefreshAsync();await rejectAsync(()=>timedSession.SelectProfileAsync(1),"Profile timeout ignored");
            check(timedSession.WriteFaulted&&timed.Writes==1,"Profile timeout stops after one command");
        }
        bool denyBackup=false;
        using(var backed=new FakeTransport())
        using(var backedSession=new KeyboardSession(backed,_=>denyBackup?Task.FromException(new IOException("disk full")):Task.CompletedTask))
        {
            await backedSession.RefreshAsync();denyBackup=true;
            await rejectAsync(()=>backedSession.SelectProfileAsync(1),"Selection without backup allowed");
            check(backed.Writes==0&&!backedSession.WriteFaulted,"Backup failure happens before profile write");
        }
        using(var stale=new FakeTransport())
        using(var staleSession=new KeyboardSession(stale,_=>Task.CompletedTask))
        {
            await staleSession.RefreshAsync();stale.Blocks[3][10]=2;
            await rejectAsync(()=>staleSession.SelectProfileAsync(1),"External physical profile change ignored");
            check(stale.Writes==0,"External selection invalidates cached plan without writing");
        }
        var draft=new StudioDraft();
        foreach(int p in Enumerable.Range(1,4))
        {draft.Assign(p,"W",new("Teclado","F"+p));draft.Lighting[p]=Lighting.Default;draft.ReportRates[p]=p-1;}
        draft.Validate();
        string file=Path.Combine(Path.GetTempPath(),"K585Studio-profiles-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            draft.Save(file);var loaded=StudioDraft.Load(file);
            check(loaded.Profile(1)["W"].Value=="F1"&&loaded.Profile(4)["W"].Value=="F4"&&loaded.Lighting.Count==4&&loaded.ReportRates.Count==4,
                "All four profile drafts persist independently in schema 1");
        }
        finally{if(File.Exists(file))File.Delete(file);}
    }
}
