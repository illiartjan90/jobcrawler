using System.Text;
using System.Text.Json;
using System.Web;
using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Infrastructure;

/// <summary>
/// Import von Stellen, die der Nutzer selbst bereitstellt (kein Crawler).
/// Gedacht für Plattformen ohne erlaubten automatisierten Zugriff:
/// Indeed, jobup.ch, jobs.ch, JobScout24 u. a.
///
/// Ablage: data/imports/**/*.json  (Pfad: JobSearch:ImportPath)
/// Quelle pro Stelle: Feld "source"; fehlt es, gilt der Name des Unterordners
/// (data/imports/indeed/ → "indeed"), sonst "Import".
/// Format: [ { "source": "jobup.ch", "url": "...", "title": "...", "company": "...",
///             "location": "...", "description": "...", "publishedAt": "2026-10-01T08:00:00Z" } ]
/// Pflicht: url, title.
/// </summary>
public sealed class ManualImportSource(IConfiguration cfg, ILogger<ManualImportSource> log) : IJobSource
{
    public string Name => "Manual import";

    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Bekannte Schreibweisen für Ordner-/Quellennamen.
    static readonly Dictionary<string, string> Display = new(StringComparer.OrdinalIgnoreCase)
    {
        ["indeed"] = "Indeed",
        ["jobup"] = "jobup.ch",
        ["jobup.ch"] = "jobup.ch",
        ["jobs"] = "jobs.ch",
        ["jobs.ch"] = "jobs.ch",
        ["jobscout24"] = "JobScout24",
        ["linkedin"] = "LinkedIn"
    };

    public async Task<IReadOnlyCollection<JobPosting>> SearchAsync(CancellationToken ct = default)
    {
        var root = cfg["JobSearch:ImportPath"] ?? Path.Combine("data", "imports");
        var jobs = new List<JobPosting>();
        if (!Directory.Exists(root)) return jobs;

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var folder = Path.GetRelativePath(root, Path.GetDirectoryName(file)!);
            var folderSource = folder == "." ? null : folder.Split(Path.DirectorySeparatorChar)[0];
            try
            {
                await using var stream = File.OpenRead(file);
                var items = await JsonSerializer.DeserializeAsync<List<ImportedJob>>(stream, Json, ct) ?? [];
                foreach (var i in items)
                {
                    var job = Create(i.Source ?? folderSource, i.Url, i.Title, i.Company,
                        i.Location, i.Description, i.PublishedAt);
                    if (job is null)
                        log.LogWarning("Import {File}: Eintrag ohne gültige url/title übersprungen", file);
                    else jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Import {File} konnte nicht gelesen werden", file);
            }
        }
        return jobs;
    }

    /// <summary>Gemeinsam genutzt vom Datei-Import und vom Endpoint POST /api/jobs/import.</summary>
    public static JobPosting? Create(string? source, string? url, string? title, string? company,
        string? location, string? description, DateTimeOffset? publishedAt)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return null;

        var src = NormalizeSource(source);
        var slug = Slug(src);

        // Stabile ID: Indeed-Job-Key (jk) falls vorhanden, sonst URL ohne Fragment.
        var jk = HttpUtility.ParseQueryString(uri.Query)["jk"];
        var key = !string.IsNullOrWhiteSpace(jk) ? jk : $"{uri.GetLeftPart(UriPartial.Path)}{uri.Query}";

        return new JobPosting(
            $"{slug}:{key}", title.Trim(),
            string.IsNullOrWhiteSpace(company) ? "Unbekannt" : company.Trim(),
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            null, null, "CHF", src, uri.ToString(), publishedAt, DateTimeOffset.UtcNow);
    }

    static string NormalizeSource(string? source)
    {
        var s = source?.Trim();
        if (string.IsNullOrEmpty(s)) return "Import";
        return Display.TryGetValue(s, out var d) ? d : s;
    }

    static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.Length == 0 ? "import" : sb.ToString();
    }

    sealed record ImportedJob(string? Source, string? Url, string? Title, string? Company,
        string? Location, string? Description, DateTimeOffset? PublishedAt);
}
