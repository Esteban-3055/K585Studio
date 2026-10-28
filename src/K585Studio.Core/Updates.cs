using System.Net;
using System.Text.Json;

namespace K585Studio.Core;

public sealed record UpdateResult(string Message, Uri? Release, Version? Version);
public static class Updates
{
    public const string Repository = "Esteban-3055/K585Studio";
    public static readonly Version Current = new(0, 1, 0);
    public static UpdateResult Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            return new("La versión publicada es de prueba.", null, null);
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) throw new InvalidDataException("Versión de GitHub no reconocida.");
        // Normalize v0.1 to 0.1.0 before comparison.
        version = new(version.Major, version.Minor, Math.Max(0, version.Build));
        var url = root.GetProperty("html_url").GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" ||
            !uri.AbsolutePath.StartsWith($"/{Repository}/releases/tag/", StringComparison.Ordinal))
            throw new InvalidDataException("La actualización no pertenece al repositorio de K585 Studio.");
        return version > Current ? new($"Disponible v{version}", uri, version) : new("Tienes la versión más reciente publicada.", null, version);
    }
    public static async Task<UpdateResult> CheckAsync(HttpClient client, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("K585Studio/0.1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return new("Todavía no hay una versión estable publicada en GitHub.", null, null);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1_000_000) throw new InvalidDataException("Respuesta de actualización demasiado grande.");
        var json = await response.Content.ReadAsStringAsync(ct);
        if (json.Length > 1_000_000) throw new InvalidDataException("Respuesta de actualización demasiado grande.");
        return Parse(json);
    }
}
