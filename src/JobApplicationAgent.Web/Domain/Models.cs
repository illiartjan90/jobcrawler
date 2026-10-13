namespace JobApplicationAgent.Domain;

public sealed record JobPosting(
    string ExternalId, string Title, string Company, string? Location,
    string? Description, int? SalaryMin, int? SalaryMax, string Currency,
    string Source, string SourceUrl, DateTimeOffset? PublishedAt,
    DateTimeOffset FoundAt);

public sealed record JobMatch(int Score, IReadOnlyList<string> MatchingSkills,
    IReadOnlyList<string> MissingSkills, string Explanation);

public sealed record JobView(JobPosting Job, JobMatch Match, string Status,
    DateTimeOffset? AppliedAt = null, string? ApplicationMethod = null, string? ApplicationNote = null);

public sealed record CandidateProfile(
    string Name, string Headline, IReadOnlyList<string> Skills,
    IReadOnlyList<string> Languages, IReadOnlyList<ExperienceItem> Experience,
    IReadOnlyList<EducationItem> Education,
    string? Email = null, string? Phone = null, string? Location = null,
    IReadOnlyList<string>? Links = null);
public sealed record ExperienceItem(string Employer,string Role,string Period,IReadOnlyList<string> Skills,string Summary);
public sealed record EducationItem(string School,string Program,string Period);

public static class ApplicationStatuses {
    public const string Discovered="Discovered", Shortlisted="Shortlisted", Prepared="Prepared",
        Approved="Approved", Applied="Applied", Interview="Interview", Offer="Offer", Rejected="Rejected";
    public static readonly string[] All=[Discovered,Shortlisted,Prepared,Approved,Applied,Interview,Offer,Rejected];
}
