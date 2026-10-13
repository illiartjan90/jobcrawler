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

                return Results.File(
                    document.Bytes,
                    document.ContentType,
                    document.FileName);
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