using K585Studio.Core;

internal static class FirmwareChecks
{
    internal static async Task Run(Action<bool,string> check,Action<Action,string> reject,Func<Func<Task>,string,Task> rejectAsync,string? fixture)
    {
        reject(()=>Firmware.Inspect([]),"Empty updater accepted");
        reject(()=>Firmware.Inspect(new byte[Firmware.UpdaterBytes]),"Unknown executable shell accepted");
        reject(()=>Firmware.ValidateTarget(0x109),"Wrong keyboard USB revision accepted");
        Firmware.ValidateTarget(0x108);check(true,"Audited keyboard revision accepted");
        byte[] bundled=Firmware.ReadBundledUpdater();
        check(bundled.Length==Firmware.UpdaterBytes&&Firmware.Hash(bundled)==Firmware.BundledUpdaterSha256,"Embedded updater exactly matches the original 20200409 file");
        check(Firmware.Inspect(bundled).Identity==Firmware.Known[0],"Embedded updater has factory code and no private snapshot");
        bundled[100]^=1;
        check(Firmware.Hash(Firmware.ReadBundledUpdater())==Firmware.BundledUpdaterSha256,"Caller changes cannot corrupt another load of the embedded updater");
        byte[] input=fixture==null?Firmware.ReadBundledUpdater():await Firmware.ReadUpdaterAsync(fixture);var info=Firmware.Inspect(input);
        check(Firmware.Known.Contains(info.Identity),"OEM updater recognized without executing it");
        var rainbow=Firmware.WithRainbow(input);
        check(Firmware.Inspect(rainbow).Identity==Firmware.Known[2],"Embedded delta reconstructs exact offline rainbow candidate code");
        check(Firmware.WithRainbow(rainbow).SequenceEqual(rainbow),"Applying rainbow to the exact candidate is idempotent");
        check(rainbow.AsSpan(Firmware.PayloadOffset+0x5400,0x1f80).SequenceEqual(input.AsSpan(Firmware.PayloadOffset+0x5400,0x1f80)),"Rainbow delta carries no profile data and preserves source settings");
        var selected=Firmware.WithSelectedRain(input);
        check(Firmware.Inspect(selected).Identity==Firmware.Known[3],"Selected preset reconstructs the exact optimized candidate code");
        check(Firmware.WithSelectedRain(selected).SequenceEqual(selected),"Selected rain conversion is idempotent");
        check(Firmware.WithSelectedRain(rainbow).SequenceEqual(selected),"Original or previous rainbow converts to the same selected candidate");
        check(selected.AsSpan(Firmware.PayloadOffset+0x5400).SequenceEqual(input.AsSpan(Firmware.PayloadOffset+0x5400)),"Selected delta preserves configuration, startup data and ROM tail");
        check(selected.AsSpan(Firmware.PayloadOffset+0x35d4,0x11a).SequenceEqual(input.AsSpan(Firmware.PayloadOffset+0x35d4,0x11a))&&selected.AsSpan(Firmware.PayloadOffset+0x4d29,0x347).SequenceEqual(input.AsSpan(Firmware.PayloadOffset+0x4d29,0x347)),"Selected rain preserves effect 1 and its native palette");
        reject(()=>Firmware.WithRainbow(selected),"An unverified downgrade from selected rain to the older rainbow was accepted");
        byte[] Data(int count,int salt)=>Enumerable.Range(0,count).Select(i=>(byte)((i+salt)%251)).ToArray();
        var snapshot=new Snapshot(DateTimeOffset.UtcNow,Data(128,1),Data(256,2),Data(768,3),Data(6144,4),Data(768,5));snapshot.General[10]=0;
        for(int profile=1;profile<=4;profile++)(Lighting.Default with{Mode=1}).Bytes().CopyTo(snapshot.Illumination,(profile-1)*64);
        var original=input.ToArray();var output=Firmware.Build(input,snapshot,0x108);
        check(input.SequenceEqual(original),"Preparing firmware preserves original updater bytes");
        check(Firmware.Inspect(output).Identity==info.Identity,"Configuration injection preserves all firmware code and immutable data");
        Firmware.AssertConfiguration(output,snapshot);check(true,"Every profile, all bindings, macros, RGB and general settings embedded exactly");
        Firmware.AssertConfiguration(Firmware.Build(rainbow,snapshot,0x108),snapshot);check(true,"Rainbow preparation preserves all four profiles exactly");
        Firmware.AssertConfiguration(Firmware.Build(selected,snapshot,0x108),snapshot);check(true,"Selected preset preparation preserves all four profiles exactly");
        var selectedP2=snapshot with{Illumination=snapshot.Illumination.ToArray()};
        (Lighting.Default with{Colorful=true}).Bytes().CopyTo(selectedP2.Illumination,64);
        Firmware.AssertConfiguration(Firmware.Build(selected,selectedP2,0x108),selectedP2);check(true,"Selected mode 23 is accepted in profile 2 without silently altering its lighting fields");
        selectedP2.Illumination[128]=23;reject(()=>Firmware.Build(selected,selectedP2,0x108),"Selected mode 23 was accepted outside profile 2");
        selectedP2.Illumination[128]=20;reject(()=>Firmware.Build(selected,selectedP2,0x108),"Removed mode 20 was accepted with selected rain");
        check(output.AsSpan(0,Firmware.PayloadOffset).SequenceEqual(input.AsSpan(0,Firmware.PayloadOffset))&&output.AsSpan(Firmware.PayloadOffset+Firmware.PayloadBytes).SequenceEqual(input.AsSpan(Firmware.PayloadOffset+Firmware.PayloadBytes)),"Only updater payload changes");
        int changed=0;bool confined=true;for(int i=0;i<output.Length;i++)if(output[i]!=input[i]){changed++;confined&=i>=Firmware.PayloadOffset+0x5400&&i<Firmware.PayloadOffset+0x7380;}
        check(confined,"Every changed byte lies inside persistent settings");
        check(changed>0,"Test configuration differs from updater settings");
        var bad=input.ToArray();bad[100]^=1;reject(()=>Firmware.Inspect(bad),"Changed updater executable code accepted");
        bad=input.ToArray();bad[Firmware.PayloadOffset+0x100]^=1;reject(()=>Firmware.Inspect(bad),"Unknown firmware instructions accepted");
        reject(()=>Firmware.Build(input,snapshot with{Bindings=[]},0x108),"Incomplete snapshot accepted");
        reject(()=>Firmware.Build(input,snapshot,0x109),"Firmware prepared for wrong revision");
        snapshot.Illumination[0]=23;reject(()=>Firmware.Build(input,snapshot,0x108),"Incompatible saved effect accepted");snapshot.Illumination[0]=1;
        string root=Path.Combine(Path.GetTempPath(),"K585Studio-firmware-"+Guid.NewGuid().ToString("N"));
        try
        {
            var prepared=await Firmware.PrepareAsync(input,snapshot,0x108,root);await Firmware.VerifyPreparedAsync(prepared,snapshot);
            check(File.Exists(Path.Combine(Path.GetDirectoryName(prepared.Path)!,"manifest.json")),"Manifest accompanies private firmware backup");
            check(File.ReadAllBytes(prepared.Path).SequenceEqual(output),"Prepared updater matches pure configuration injection");
            Firmware.SavePending(prepared,root);var pending=Firmware.LoadPending(root)!;
            check(pending.Path==prepared.Path&&pending.Before.SameData(snapshot),"Pending update and expected configuration survive restart");
            await Firmware.VerifyPreparedAsync(pending,snapshot);check(true,"Restarted pending updater passes fresh verification");Firmware.ClearPending(root);
            check(Firmware.LoadPending(root)==null,"Pending update cleared only explicitly");
            snapshot.Bindings[0]^=1;await rejectAsync(()=>Firmware.VerifyPreparedAsync(prepared,snapshot),"Changed device configuration accepted at launch");snapshot.Bindings[0]^=1;
            var bytes=File.ReadAllBytes(prepared.Path);bytes[100]^=1;File.WriteAllBytes(prepared.Path,bytes);
            await rejectAsync(()=>Firmware.VerifyPreparedAsync(prepared,snapshot),"Modified prepared executable accepted");File.WriteAllBytes(prepared.Path,output);
            string backup=Path.Combine(Path.GetDirectoryName(prepared.Path)!,"macros.bin");File.Delete(backup);
            await rejectAsync(()=>Firmware.VerifyPreparedAsync(prepared,snapshot),"Missing backup ignored");
            using var canceled=new CancellationTokenSource();canceled.Cancel();
            bool cancellationObserved=false;try{await Firmware.ReadUpdaterAsync(prepared.Path,canceled.Token);}catch(OperationCanceledException){cancellationObserved=true;}
            check(cancellationObserved,"Canceled updater read stops with cancellation");
        }
        finally
        {
            // Only delete the exact isolated directory created by this test.
            string resolved=Path.GetFullPath(root),parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if(Path.GetDirectoryName(resolved)!=parent||!Path.GetFileName(resolved).StartsWith("K585Studio-firmware-",StringComparison.Ordinal))throw new IOException("Unexpected test cleanup path.");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
}
