using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace K585Studio.Core;

public sealed record UpdateResult(string Message, Uri? Release, Version? Version, InstallerUpdate? Installer = null);
public sealed record InstallerUpdate(Version Version, Uri Download, long Size, string Sha256);
public static class Updates
{
    public const string Repository = "Esteban-3055/K585Studio";
    public const long MaximumInstallerBytes = 256L * 1024 * 1024;
    public static readonly Version Current = new(0, 3, 0);
    public static string InstallerName(Version version) => $"K585Studio-{version}-Setup-win-x64.exe";
    public static UpdateResult Parse(string json) => Parse(json, Current);
    public static UpdateResult Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return new("No hay una nueva versión estable.", null, null);
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, "^v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$") || !Version.TryParse(tag[1..], out var version)) throw new InvalidDataException("Versión de GitHub no reconocida.");
        version = new(version.Major,version.Minor,Math.Max(0,version.Build));
        string expectedPage=$"https://github.com/{Repository}/releases/tag/{tag}";
        if(root.GetProperty("html_url").GetString()!=expectedPage)throw new InvalidDataException("La actualización no pertenece a la versión publicada del repositorio.");
        if(version<=current)return new("Tienes la versión estable más reciente.",null,version);
        string name=InstallerName(version); string url=$"https://github.com/{Repository}/releases/download/{tag}/{name}";
        var assets=root.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("name").GetString()==name).ToArray();
        if(assets.Length!=1)throw new InvalidDataException("La versión publicada no contiene un instalador único compatible.");
        var asset=assets[0];long size=asset.GetProperty("size").GetInt64();string digest=asset.GetProperty("digest").GetString()??"";
        if(asset.GetProperty("state").GetString()!="uploaded"||asset.GetProperty("browser_download_url").GetString()!=url||size<=0||size>MaximumInstallerBytes||!Regex.IsMatch(digest,"^sha256:[0-9a-f]{64}$"))
            throw new InvalidDataException("El instalador no tiene URL, tamaño o SHA-256 válidos en GitHub.");
        return new($"Disponible v{version}",new Uri(expectedPage),version,new(version,new Uri(url),size,digest[7..]));
    }
    private static HttpRequestMessage Request(string url)
    {
        var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("K585Studio/"+Current);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");request.Headers.Add("X-GitHub-Api-Version","2022-11-28");return request;
    }
    public static async Task<UpdateResult> CheckAsync(HttpClient client,CancellationToken ct=default)
    {
        using var request=Request($"https://api.github.com/repos/{Repository}/releases/latest");
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode==HttpStatusCode.NotFound)return new("Todavía no hay una versión estable publicada.",null,null);
        response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();
        await CopyBounded(input,output,2*1024*1024,null,ct);return Parse(Encoding.UTF8.GetString(output.ToArray()));
    }
    public static void Validate(InstallerUpdate update)
    {
        string url=$"https://github.com/{Repository}/releases/download/v{update.Version}/{InstallerName(update.Version)}";
        if(update.Version<=Current||update.Download.AbsoluteUri!=url||update.Size<=0||update.Size>MaximumInstallerBytes||!Regex.IsMatch(update.Sha256,"^[0-9a-f]{64}$"))throw new InvalidDataException("Instalador de actualización no válido.");
    }
    public static async Task<string> DownloadAsync(HttpClient client,InstallerUpdate update,string cache,IProgress<int>? progress=null,CancellationToken ct=default)
    {
        Validate(update);Directory.CreateDirectory(cache);
        if((File.GetAttributes(cache)&FileAttributes.ReparsePoint)!=0)throw new IOException("La carpeta de actualizaciones no puede ser un enlace.");
        string destination=Path.Combine(cache,InstallerName(update.Version)),partial=Path.Combine(cache,Guid.NewGuid().ToString("N")+".partial");
        try
        {
            using var request=Request(update.Download.AbsoluteUri);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
            var final=response.RequestMessage?.RequestUri;
            if(final==null||final.Scheme!="https"||!(final.Host=="github.com"||final.Host=="release-assets.githubusercontent.com"||final.Host=="objects.githubusercontent.com"))throw new InvalidDataException("La descarga fue redirigida fuera de GitHub.");
            if(response.Content.Headers.ContentLength is {} length&&length!=update.Size)throw new InvalidDataException("Tamaño publicado y descargado diferentes.");
            await using(var input=await response.Content.ReadAsStreamAsync(ct))
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))
                await CopyBounded(input,output,update.Size,progress==null?null:new Progress<long>(n=>progress.Report((int)(n*100/update.Size))),ct);
            await VerifyFileAsync(update,partial,ct);File.Move(partial,destination,true);return destination;
        }
        catch { if(File.Exists(partial))File.Delete(partial);throw; }
    }
    public static async Task VerifyFileAsync(InstallerUpdate update,string path,CancellationToken ct=default)
    {
        Validate(update);
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("El instalador no puede ser un enlace.");
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous);
        if(stream.Length!=update.Size)throw new InvalidDataException("El tamaño del instalador ha cambiado.");
        string hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct)).ToLowerInvariant();
        if(hash!=update.Sha256)throw new InvalidDataException("La suma SHA-256 del instalador no coincide. Vuelve a descargar.");
    }
    public static async Task ConfirmPublishedAsync(HttpClient client,InstallerUpdate update,CancellationToken ct=default)
    {
        Validate(update);using var request=Request($"https://api.github.com/repos/{Repository}/releases/tags/v{update.Version}");
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var memory=new MemoryStream();await CopyBounded(input,memory,2*1024*1024,null,ct);
        if(Parse(Encoding.UTF8.GetString(memory.ToArray())).Installer!=update)throw new InvalidDataException("La publicación cambió después de descargarla. Busca actualizaciones otra vez.");
    }
    internal static async Task CopyBounded(Stream input,Stream output,long maximum,IProgress<long>? progress,CancellationToken ct)
    {
        byte[] buffer=new byte[81920];long total=0;int count;
        while((count=await input.ReadAsync(buffer,ct))!=0){total+=count;if(total>maximum)throw new InvalidDataException("La descarga supera el tamaño permitido.");await output.WriteAsync(buffer.AsMemory(0,count),ct);progress?.Report(total);}
    }
}
