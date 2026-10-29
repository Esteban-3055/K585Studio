using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace K585Studio.Core;

public sealed record FirmwareIdentity(string Name,string CodeSha256,bool Experimental);
public sealed record FirmwareInspection(FirmwareIdentity Identity,string UpdaterSha256,string FirmwareSha256);
public sealed record FirmwarePrepared(string Path,string Sha256,Snapshot Before,FirmwareInspection Inspection);

/// <summary>The audited OEM updater is the only programming backend. No flash commands are implemented here.</summary>
public static class Firmware
{
    public const int UpdaterBytes=2870272,PayloadOffset=2686512,PayloadBytes=65536;
    public const ushort SupportedRevision=0x0108;
    private const int ConfigurationStart=0x5400,ConfigurationEnd=0x7380;
    private const string ShellSha256="7672220446afb88675a9e67fe3f3f49d895aede9cad1ef74c05fc48d1721535c";
    public static readonly IReadOnlyList<FirmwareIdentity> Known=Array.AsReadOnly(new[]
    {
        new FirmwareIdentity("Original K585 · 20200409","5e10f91d2aa37ac938b4b11aacc565b7a106e39d9b25e7836388b3e11de2fce5",false),
        new FirmwareIdentity("Lluvia · Serpenteo","c626f72e6f901f9b50c13ae9cd4f0b78241d6d3a9dff5bc7a711cce8466905dc",false),
        new FirmwareIdentity("Lluvia · Arcoíris en la estela (experimental)","42c23b381019a6d0ce5c182e502a6625391c283d280fdbc5bb40b0df9c9938b0",true)
    });
    public static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static FirmwareInspection Inspect(byte[] updater)
    {
        if(updater.Length!=UpdaterBytes)throw new InvalidDataException("El archivo no tiene el tamaño del actualizador K585 estudiado.");
        var shell=updater.ToArray();shell.AsSpan(PayloadOffset,PayloadBytes).Clear();
        if(Hash(shell)!=ShellSha256)throw new InvalidDataException("El ejecutable no coincide con el actualizador K585 auditado. No se ejecutará.");
        var code=updater.AsSpan(PayloadOffset,PayloadBytes).ToArray();code.AsSpan(ConfigurationStart,ConfigurationEnd-ConfigurationStart).Clear();
        string digest=Hash(code);var identity=Known.SingleOrDefault(x=>x.CodeSha256==digest)
            ??throw new InvalidDataException("La versión de firmware no está reconocida por Studio. No se ejecutará.");
        return new(identity,Hash(updater),Hash(updater.AsSpan(PayloadOffset,PayloadBytes)));
    }
    public static async Task<byte[]> ReadUpdaterAsync(string path,CancellationToken ct=default)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Selecciona un archivo local de actualizador.");
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous);
        if(stream.Length!=UpdaterBytes)throw new InvalidDataException("Tamaño de actualizador K585 no reconocido.");
        byte[] bytes=new byte[UpdaterBytes];await stream.ReadExactlyAsync(bytes,ct);return bytes;
    }
    public static byte[] WithRainbow(byte[] updater)
    {
        var source=Inspect(updater);if(source.Identity==Known[2])return updater.ToArray();
        using var stream=typeof(Firmware).Assembly.GetManifestResourceStream("K585Studio.Core.RainbowPatch.json")??throw new InvalidDataException("Falta la modificación de lluvia.");
        using var document=JsonDocument.Parse(stream);var root=document.RootElement;
        if(root.GetProperty("schema").GetInt32()!=1||root.GetProperty("targetCodeSha256").GetString()!=Known[2].CodeSha256)throw new InvalidDataException("Versión de modificación no reconocida.");
        var plan=root.GetProperty("plans").EnumerateArray().Single(x=>x.GetProperty("sourceCodeSha256").GetString()==source.Identity.CodeSha256);
        byte[] result=updater.ToArray();
        foreach(var patch in plan.GetProperty("patches").EnumerateArray())
        {
            int offset=patch.GetProperty("offset").GetInt32();byte[] bytes=Convert.FromHexString(patch.GetProperty("hex").GetString()!);
            if(offset<0x100||bytes.Length==0||offset>ConfigurationStart-bytes.Length)throw new InvalidDataException("La modificación intenta salir de sus regiones de código.");
            bytes.CopyTo(result,PayloadOffset+offset);
        }
        if(Inspect(result).Identity!=Known[2]||!updater.AsSpan(PayloadOffset+ConfigurationStart,ConfigurationEnd-ConfigurationStart).SequenceEqual(result.AsSpan(PayloadOffset+ConfigurationStart,ConfigurationEnd-ConfigurationStart)))
            throw new InvalidDataException("El resultado no coincide con el candidato de lluvia verificado.");
        return result;
    }
    public static void ValidateTarget(ushort revision)
    {
        if(revision!=SupportedRevision)throw new InvalidDataException($"La revisión USB 0x{revision:X4} no coincide con 0x0108, la revisión estudiada.");
    }
    private static void ValidateSnapshot(Snapshot snapshot)
    {
        if(snapshot.General.Length!=128||snapshot.Illumination.Length!=256||snapshot.Bindings.Length!=768||snapshot.Rgb.Length!=768||snapshot.Macros.Length!=6144||snapshot.ActiveProfile is <1 or >4)
            throw new InvalidDataException("El respaldo no contiene todos los bloques de configuración del K585.");
    }
    private static (string Name,int Offset,byte[] Data)[] Blocks(Snapshot snapshot)=>
    [ ("general",0x5400,snapshot.General),("illumination",0x5480,snapshot.Illumination),
      ("bindings",0x5580,snapshot.Bindings),("rgb",0x5880,snapshot.Rgb),("macros",0x5b80,snapshot.Macros) ];
    public static byte[] Build(byte[] source,Snapshot snapshot,ushort revision)
    {
        ValidateTarget(revision);ValidateSnapshot(snapshot);var before=Inspect(source);var output=source.ToArray();
        bool original=before.Identity==Known[0];
        for(int profile=1;profile<=4;profile++)
        {
            byte mode=snapshot.Profile(profile).Mode;
            if(original&&mode==23)throw new InvalidDataException($"El firmware original no incluye lluvia 23. Cambia primero el perfil {profile} a un efecto original y vuelve a preparar.");
            if(!original&&(mode==20||mode==23&&profile!=2))throw new InvalidDataException($"El perfil {profile} usa un efecto incompatible con esta lluvia: el 20 fue retirado y el 23 funciona únicamente en perfil 2. Cambia ese efecto antes de preparar.");
        }
        foreach(var block in Blocks(snapshot))block.Data.CopyTo(output,PayloadOffset+block.Offset);
        if(Inspect(output).Identity!=before.Identity)throw new InvalidDataException("La preparación alteró el código del firmware.");
        AssertConfiguration(output,snapshot);return output;
    }
    public static void AssertConfiguration(byte[] updater,Snapshot snapshot)
    {
        ValidateSnapshot(snapshot);Inspect(updater);
        foreach(var block in Blocks(snapshot))
            if(!updater.AsSpan(PayloadOffset+block.Offset,block.Data.Length).SequenceEqual(block.Data))
                throw new InvalidDataException("El paquete no conserva la configuración actual: "+block.Name+".");
    }
    public static ushort Checksum(byte[] updater)
    {
        Inspect(updater);uint sum=0;
        for(int i=0;i<0x7800;i+=2)sum+=BinaryPrimitives.ReadUInt16LittleEndian(updater.AsSpan(PayloadOffset+i,2));
        return (ushort)sum;
    }
    public static async Task<FirmwarePrepared> PrepareAsync(byte[] source,Snapshot snapshot,ushort revision,string root,CancellationToken ct=default)
    {
        byte[] output=Build(source,snapshot,revision);
        // Own the expected data; a caller mutating its snapshot must not alter the launch check.
        var saved=snapshot with{General=snapshot.General.ToArray(),Illumination=snapshot.Illumination.ToArray(),Bindings=snapshot.Bindings.ToArray(),Rgb=snapshot.Rgb.ToArray(),Macros=snapshot.Macros.ToArray()};
        Directory.CreateDirectory(root);
        if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("La carpeta de firmware no puede ser un enlace.");
        string folder=Path.Combine(root,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"Update_Firmware.exe");var inspection=Inspect(output);
        // The complete backup must be durable before an executable is made available.
        foreach(var block in Blocks(saved))await File.WriteAllBytesAsync(Path.Combine(folder,block.Name+".bin"),block.Data,ct);
        var manifest=new{schema=1,app=Updates.Current.ToString(),createdUtc=DateTimeOffset.UtcNow,revision=$"0x{revision:X4}",
            firmware=inspection.Identity,inspection.UpdaterSha256,inspection.FirmwareSha256,snapshotDigest=saved.Digest,
            checksum=$"{Checksum(output):X4}",blocks=Blocks(saved).ToDictionary(b=>b.Name,b=>new{bytes=b.Data.Length,sha256=Hash(b.Data)}),
            launched=false,hardwareFirmwareVerified=false};
        await File.WriteAllTextAsync(Path.Combine(folder,"manifest.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}),ct);
        await using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.WriteThrough))
        {await stream.WriteAsync(output,ct);await stream.FlushAsync(ct);}
        var prepared=new FirmwarePrepared(file,inspection.UpdaterSha256,saved,inspection);await VerifyPreparedAsync(prepared,saved,ct);return prepared;
    }
    public static async Task VerifyPreparedAsync(FirmwarePrepared prepared,Snapshot current,CancellationToken ct=default)
    {
        if(!prepared.Before.SameData(current))throw new InvalidDataException("El teclado cambió después de preparar el paquete. Prepáralo de nuevo antes de abrir el actualizador.");
        byte[] bytes=await ReadUpdaterAsync(prepared.Path,ct);
        if(Hash(bytes)!=prepared.Sha256||Inspect(bytes)!=prepared.Inspection)throw new InvalidDataException("El actualizador preparado cambió. Vuelve a preparar el paquete.");
        AssertConfiguration(bytes,current);
        string folder=Path.GetDirectoryName(prepared.Path)!;
        foreach(var block in Blocks(prepared.Before))
            if(!(await File.ReadAllBytesAsync(Path.Combine(folder,block.Name+".bin"),ct)).AsSpan().SequenceEqual(block.Data))
                throw new InvalidDataException("El respaldo de configuración está incompleto o cambió. Prepara otro paquete.");
    }
    public static void SavePending(FirmwarePrepared prepared,string root)
    {
        Directory.CreateDirectory(root);string path=Path.Combine(root,"pending.json"),temporary=Path.Combine(root,Guid.NewGuid().ToString("N")+".tmp");
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(prepared));File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static FirmwarePrepared? LoadPending(string root)
    {
        string path=Path.Combine(root,"pending.json");if(!File.Exists(path))return null;
        using var stream=File.OpenRead(path);
        if(stream.Length>2*1024*1024)throw new InvalidDataException("El registro de firmware pendiente supera el límite.");
        var pending=JsonSerializer.Deserialize<FirmwarePrepared>(stream)??throw new InvalidDataException("Registro de firmware pendiente incompleto.");
        string relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(pending.Path));
        if(Path.IsPathRooted(relative)||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar))throw new InvalidDataException("El paquete pendiente no está en la carpeta privada de firmware.");
        ValidateSnapshot(pending.Before);return pending;
    }
    public static void ClearPending(string root)=>File.Delete(Path.Combine(root,"pending.json"));
}
