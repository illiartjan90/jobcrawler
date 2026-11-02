using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Tests;

internal static class TestData
{
    public static JobPosting Job(
        string title = "Senior Software Engineer",
        string company = "Unbekannt",
        string source = "SwissDevJobs",
        string? description = null) =>
        new(
            ExternalId: "test-1",
            Title: title,
            Company: company,
            Location: "Zürich",
            Description: description,
            SalaryMin: null,
            SalaryMax: null,
            Currency: "CHF",
            Source: source,
            SourceUrl: "https://example.com/job/1",
            PublishedAt: null,
            FoundAt: DateTimeOffset.UnixEpoch);
}
