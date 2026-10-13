using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Web;

public static class Endpoints
{
    public static WebApplication MapJobApplicationEndpoints(
        this WebApplication app)
    {
        // ------------------------------------------------------------
        // Health
        // ------------------------------------------------------------

        app.MapGet(
            "/api/health",
            () => Results.Ok(new
            {
                status = "ok",
                time = DateTimeOffset.UtcNow
            }));


        // ------------------------------------------------------------
        // Jobs auflisten
        // ------------------------------------------------------------

        app.MapGet(
            "/api/jobs",
            async (
                string? q,
                int? minScore,
                string? status,
                IJobRepository repo,
                CancellationToken ct) =>
            {
                var jobs = await repo.ListAsync(
                    q,
                    minScore ?? 0,
                    status,
                    ct);

                return Results.Ok(jobs);
            });


        // ------------------------------------------------------------
        // Einzelnen Job laden
        //
        // Wichtig:
        // ExternalId kann eine komplette URL enthalten.
        // Deshalb Query-Parameter statt /api/jobs/{id}.
        // ------------------------------------------------------------

        app.MapGet(
            "/api/job",
            async (
                string id,
                IJobRepository repo,
                CancellationToken ct) =>
            {
                var job = await repo.GetAsync(id, ct);

                return job is null
                    ? Results.NotFound()
                    : Results.Ok(job);
            });


        // ------------------------------------------------------------
        // Jobquellen aktualisieren
        // ------------------------------------------------------------

        app.MapPost(
            "/api/jobs/scan",
            async (
                JobScanService scanner,
                CancellationToken ct) =>
            {
                var count = await scanner.ScanAsync(ct);

                return Results.Ok(new
                {
                    count
                });
            });


        // ------------------------------------------------------------
        // Einzelne Stelle manuell importieren (z. B. Indeed/jobup.ch-Link + Text)
        // ------------------------------------------------------------

        app.MapPost(
            "/api/jobs/import",
            async (
                ImportJobRequest request,
                IJobRepository repo,
                IJobMatcher matcher,
                IEnumerable<IJobNormalizer> normalizers,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                var job = Infrastructure.ManualImportSource.Create(
                    request.Source,
                    request.Url,
                    request.Title,
                    request.Company,
                    request.Location,
                    request.Description,
                    request.PublishedAt);

                if (job is null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Url (http/https) und Title sind Pflicht."
                    });
                }

                job = normalizers.Aggregate(job, (j, n) => n.Normalize(j));

                await repo.UpsertAsync(job, matcher.Match(job, profile), ct);

                return Results.Ok(new { id = job.ExternalId });
            });


        // ------------------------------------------------------------
        // Status ändern
        // ------------------------------------------------------------

        app.MapPut(
            "/api/job/status",
            async (
                string id,
                StatusRequest request,
                IJobRepository repo,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    return Results.BadRequest(new
                    {
                        error = "Status fehlt."
                    });
                }

                if (!ApplicationStatuses.All.Contains(
                        request.Status,
                        StringComparer.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new
                    {
                        error = $"Ungültiger Status: {request.Status}"
                    });
                }

                var existing = await repo.GetAsync(id, ct);

                if (existing is null)
                    return Results.NotFound();

                var normalizedStatus =
                    ApplicationStatuses.All.First(x =>
                        x.Equals(
                            request.Status,
                            StringComparison.OrdinalIgnoreCase));

                await repo.SetStatusAsync(
                    id,
                    normalizedStatus,
                    ct);

                return Results.NoContent();
            });


        // ------------------------------------------------------------
        // Bewerbungsdetails (Art + Notiz) speichern
        // ------------------------------------------------------------

        app.MapPut(
            "/api/job/application",
            async (
                string id,
                ApplicationDetailsRequest request,
                IJobRepository repo,
                CancellationToken ct) =>
            {
                var existing = await repo.GetAsync(id, ct);

                if (existing is null)
                    return Results.NotFound();

                await repo.SetApplicationDetailsAsync(
                    id,
                    request.Method,
                    request.Note,
                    ct);

                return Results.NoContent();
            });


        // ------------------------------------------------------------
        // Statistik
        // ------------------------------------------------------------

        app.MapGet(
            "/api/stats",
            async (
                IJobRepository repo,
                CancellationToken ct) =>
            {
                var stats = await repo.StatsAsync(ct);

                return Results.Ok(stats);
            });


        // ------------------------------------------------------------
        // CV erstellen
        // ------------------------------------------------------------

        app.MapGet(
            "/api/job/cv",
            async (
                string id,
                string? format,
                IJobRepository repo,
                IDocumentService documents,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                var job = await repo.GetAsync(id, ct);

                if (job is null)
                    return Results.NotFound();

                var document =
                    await documents.CreateCvAsync(
                        job,
                        profile,
                        format ?? "docx");

                return Results.File(
                    document.Bytes,
                    document.ContentType,
                    document.FileName);
            });


        // ------------------------------------------------------------
        // Anschreiben erstellen
        // ------------------------------------------------------------

        app.MapGet(
            "/api/job/cover-letter",
            async (
                string id,
                string? format,
                HttpContext http,
                IJobRepository repo,
                IDocumentService documents,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                var job = await repo.GetAsync(id, ct);

                if (job is null)
                    return Results.NotFound();

                var document =
                    await documents.CreateCoverLetterAsync(
                        job,
                        profile,
                        format ?? "docx");

                http.Response.Headers["X-Letter-Source"] = document.Source;

                return Results.File(
                    document.Bytes,
                    document.ContentType,
                    document.FileName);
            });


        // ------------------------------------------------------------
        // KI-Analyse (vom Dashboard-Button "KI-Analyse starten" genutzt)
        // ------------------------------------------------------------

        app.MapPost(
            "/api/job/ai-analysis",
            async (
                string id,
                IJobRepository repo,
                IAiService ai,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                var view = await repo.GetAsync(id, ct);

                if (view is null)
                    return Results.NotFound();

                if (!ai.IsEnabled)
                {
                    return Results.Ok(new
                    {
                        analysis =
                            "KI ist deaktiviert. Setze die Umgebungsvariable OPENAI_API_KEY " +
                            "und in appsettings.json OpenAI:Enabled=true, dann neu starten."
                    });
                }

                var input =
                    $"""
                    KANDIDATENPROFIL
                    Headline: {profile.Headline}
                    Skills: {string.Join(", ", profile.Skills)}
                    Sprachen: {string.Join(", ", profile.Languages)}
                    Erfahrung:
                    {string.Join("\n", profile.Experience.Select(e => $"- {e.Role}, {e.Employer} ({e.Period}): {string.Join(", ", e.Skills)}"))}
                    Ausbildung:
                    {string.Join("\n", profile.Education.Select(e => $"- {e.Program}, {e.School} ({e.Period})"))}

                    STELLE
                    Titel: {view.Job.Title}
                    Firma: {view.Job.Company}
                    Ort: {view.Job.Location}
                    Beschreibung:
                    {Cut(view.Job.Description, 2500)}

                    REGELBASIERTES MATCHING
                    Score: {view.Match.Score}
                    Passend: {string.Join(", ", view.Match.MatchingSkills)}
                    Fehlend: {string.Join(", ", view.Match.MissingSkills)}
                    """;

                try
                {
                    var analysis = await ai.GenerateAsync(
                        """
                        Analysiere auf Deutsch, wie gut das Kandidatenprofil zur Stelle passt.
                        Gliedere knapp in: Passung, Lücken, Empfehlung (bewerben ja/nein, warum),
                        Hinweise fürs Anschreiben. Antworte in höchstens 150 Wörtern.
                        Verwende ausschliesslich Fakten aus dem Kandidatenprofil.
                        Erfinde keine Erfahrung, Skills oder Abschlüsse.
                        """,
                        input,
                        ct);

                    return Results.Ok(new { analysis });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });


        // ------------------------------------------------------------
        // Bewerbung per E-Mail senden (CV + Anschreiben als PDF)
        //
        // Sendet nur mit confirm = true und einer vom Nutzer
        // eingegebenen Empfängeradresse. Danach Status "Applied".
        // ------------------------------------------------------------

        app.MapPost(
            "/api/job/apply",
            async (
                string id,
                ApplyRequest request,
                IJobRepository repo,
                IDocumentService documents,
                IApplicationMailer mailer,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                if (!request.Confirm)
                {
                    return Results.BadRequest(new
                    {
                        error = "Versand nicht bestätigt."
                    });
                }

                if (!System.Net.Mail.MailAddress.TryCreate(request.To, out var recipient))
                {
                    return Results.BadRequest(new
                    {
                        error = "Ungültige Empfänger-E-Mail-Adresse."
                    });
                }

                if (!mailer.IsEnabled)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "E-Mail-Versand ist nicht eingerichtet. " +
                            "Smtp:Enabled=true, Smtp:Host/User/From in appsettings.json " +
                            "und Umgebungsvariable SMTP_PASSWORD setzen."
                    });
                }

                var view = await repo.GetAsync(id, ct);

                if (view is null)
                    return Results.NotFound();

                var cv = await documents.CreateCvAsync(view, profile, "pdf");
                var letter = await documents.CreateCoverLetterAsync(view, profile, "pdf");

                var subject = string.IsNullOrWhiteSpace(request.Subject)
                    ? $"Bewerbung als {view.Job.Title} – {profile.Name}"
                    : request.Subject.Trim();

                var body = string.IsNullOrWhiteSpace(request.Body)
                    ? $"Sehr geehrte Damen und Herren\n\n" +
                      $"anbei erhalten Sie meine Bewerbungsunterlagen für die Stelle " +
                      $"{view.Job.Title}.\n\nFreundliche Grüsse\n{profile.Name}"
                    : request.Body;

                try
                {
                    await mailer.SendAsync(
                        recipient.Address,
                        subject,
                        body,
                        [
                            new MailAttachment(cv.FileName, cv.Bytes, cv.ContentType),
                            new MailAttachment(letter.FileName, letter.Bytes, letter.ContentType)
                        ],
                        ct);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        error = "Versand fehlgeschlagen: " + ex.Message
                    });
                }

                await repo.SetStatusAsync(id, ApplicationStatuses.Applied, ct);

                await repo.SetApplicationDetailsAsync(
                    id,
                    "E-Mail",
                    $"An {recipient.Address} gesendet. Anschreiben: {letter.Source}.",
                    ct);

                return Results.Ok(new { sentTo = recipient.Address });
            });


        // ------------------------------------------------------------
        // Requirements + Kandidaten-Abgleich
        //
        // Dieser Endpoint existiert ABSICHTLICH nur einmal.
        // ------------------------------------------------------------

        app.MapGet(
            "/api/job/requirements",
            async (
                string id,
                IJobRepository repo,
                JobRequirementAnalyzer analyzer,
                CandidateRequirementMatcher matcher,
                CandidateProfile profile,
                CancellationToken ct) =>
            {
                var view = await repo.GetAsync(id, ct);

                if (view is null)
                    return Results.NotFound();

                var requirements =
                    analyzer.Analyze(view.Job);

                var matches =
                    matcher.Match(
                        requirements,
                        profile);

                return Results.Ok(matches);
            });


        // ------------------------------------------------------------
        // Debug
        // ------------------------------------------------------------

        app.MapGet(
            "/api/debug/jobs",
            async (
                IJobRepository repo,
                CancellationToken ct) =>
            {
                var jobs = await repo.ListAsync(
                    null,
                    0,
                    null,
                    ct);

                var result = jobs.Select(x => new
                {
                    id = x.Job.ExternalId,
                    title = x.Job.Title,
                    company = x.Job.Company,
                    location = x.Job.Location,
                    source = x.Job.Source,
                    description = x.Job.Description,
                    matchingSkills = x.Match.MatchingSkills,
                    missingSkills = x.Match.MissingSkills,
                    score = x.Match.Score,
                    status = x.Status
                });

                return Results.Ok(result);
            });


        return app;
    }


    private static string Cut(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + " …";

    public sealed record ApplicationDetailsRequest(
        string? Method,
        string? Note);

    public sealed record ApplyRequest(
        string? To,
        string? Subject,
        string? Body,
        bool Confirm);

    public sealed record StatusRequest(
        string Status);

    public sealed record ImportJobRequest(
        string? Source,
        string? Url,
        string? Title,
        string? Company,
        string? Location,
        string? Description,
        DateTimeOffset? PublishedAt);
}