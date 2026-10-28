using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using K585Studio.Core;

internal static class UpdateChecks
{
    internal static async Task Run(Action<bool,string> check,Func<Func<Task>,string,Task> reject)
    {
        byte[] payload=[11,22,33,44];string hash=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string url="https://github.com/Esteban-3055/K585Studio/releases/download/v0.3.0/K585Studio-0.3.0-Setup-win-x64.exe";
        string Release(long size,string digest,string download,int count=1) => JsonSerializer.Serialize(new{draft=false,prerelease=false,tag_name="v0.3.0",html_url="https://github.com/Esteban-3055/K585Studio/releases/tag/v0.3.0",assets=Enumerable.Range(0,count).Select(_=>new{name="K585Studio-0.3.0-Setup-win-x64.exe",size,digest,state="uploaded",browser_download_url=download})});
        var update=Updates.Parse(Release(payload.Length,"sha256:"+hash,url)).Installer!;
        await reject(()=>Task.Run(()=>Updates.Parse(Release(0,"sha256:"+hash,url))),"Zero-size installer accepted");
        await reject(()=>Task.Run(()=>Updates.Parse(Release(payload.Length,"sha256:"+hash,"https://evil.example/setup.exe"))),"Wrong installer host accepted");
        await reject(()=>Task.Run(()=>Updates.Parse(Release(payload.Length,"sha256:"+hash,url,2))),"Duplicate installer accepted");
        await reject(()=>Task.Run(()=>Updates.Parse(Release(payload.Length,"bad",url))),"Missing digest accepted");
        using var client=new HttpClient(new FakeHttp(request=>new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=request.RequestUri!.Host=="api.github.com"?new StringContent(Release(payload.Length,"sha256:"+hash,url)):new ByteArrayContent(payload)}));
        string folder=Path.Combine(Path.GetTempPath(),"K585Studio-Update-"+Guid.NewGuid().ToString("N"));
        try
        {
            string downloaded=await Updates.DownloadAsync(client,update,folder);
            check(File.ReadAllBytes(downloaded).SequenceEqual(payload),"Download preserves bytes");
            await Updates.VerifyFileAsync(update,downloaded);check(true,"Downloaded installer hash verified");
            await Updates.ConfirmPublishedAsync(client,update);check(true,"Fresh release metadata matches download");
            File.WriteAllBytes(downloaded,[11,22,33,45]);await reject(()=>Updates.VerifyFileAsync(update,downloaded),"Modified cache accepted");
            using var changed=new HttpClient(new FakeHttp(request=>new(HttpStatusCode.OK){RequestMessage=request,Content=new StringContent(Release(4,"sha256:"+new string('b',64),url))}));
            await reject(()=>Updates.ConfirmPublishedAsync(changed,update),"Changed release metadata accepted");
            using var truncated=new HttpClient(new FakeHttp(request=>new(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent([1,2])}));
            await reject(()=>Updates.DownloadAsync(truncated,update,folder),"Truncated download accepted");
            check(!Directory.GetFiles(folder,"*.partial").Any(),"Failed download leaves no partial executable");
            using var oldRelease=new HttpClient(new FakeHttp(request=>new(HttpStatusCode.NotFound){RequestMessage=request}));
            check((await Updates.CheckAsync(oldRelease)).Installer==null,"No release is handled without an installer");
            using var forbidden=new HttpClient(new FakeHttp(request=>new(HttpStatusCode.Forbidden){RequestMessage=request}));
            await reject(()=>Updates.CheckAsync(forbidden),"HTTP failure treated as current release");
        }
        finally{if(Directory.Exists(folder)){foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}}
    }
    private sealed class FakeHttp(Func<HttpRequestMessage,HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(response(request));}
    }
}
