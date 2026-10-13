using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddJobAgent(
        this IServiceCollection s,
        IConfiguration cfg)
    {
        var cs =
            cfg.GetConnectionString("Jobs")
            ?? "Data Source=data/jobagent.db";

        var profilePath = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "candidate-profile.json");

        // Candidate master profile
        s.AddSingleton(
            CandidateProfileLoader.Load(profilePath));

        // Analysis + matching
        s.AddSingleton<JobRequirementAnalyzer>();
        s.AddSingleton<CandidateRequirementMatcher>();
        s.AddSingleton<IJobMatcher, JobMatcher>();

        // Normalization + persistence
        s.AddSingleton<IJobNormalizer, SwissDevJobsNormalizer>();
        s.AddSingleton<IJobRepository>(
            _ => new SqliteJobRepository(cs));

        // External sources / AI
        s.AddHttpClient<IJobSource, SwissDevJobsSource>();
        s.AddSingleton<IJobSource, ManualImportSource>(); // nutzerbereitgestellte Importe, kein Crawler
        s.AddHttpClient<IAiService, OpenAiService>(c => c.Timeout = TimeSpan.FromMinutes(5)); // lokale Modelle antworten langsam
        s.AddSingleton<IApplicationMailer, SmtpMailer>();

        // Documents
        s.AddSingleton<IDocumentService, DocumentService>();

        // Scanning
        s.AddScoped<JobScanService>();
        s.AddHostedService<BackgroundScanner>();

        return s;
    }

    public static async Task InitializeJobAgentAsync(
        this IServiceProvider sp)
    {
        var repo =
            (SqliteJobRepository)sp.GetRequiredService<IJobRepository>();

        await repo.InitializeAsync();
    }
}