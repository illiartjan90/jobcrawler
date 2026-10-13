using System.Text.RegularExpressions;
using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;
namespace JobApplicationAgent.Infrastructure;

public sealed partial class SwissDevJobsNormalizer : IJobNormalizer
{
    public JobPosting Normalize(JobPosting job)
    {
        if (!job.Source.Equals("SwissDevJobs", StringComparison.OrdinalIgnoreCase)) return job;
        var title = job.Title.Trim(); var company = job.Company; int? min = null, max = null;
        var sm = SalaryRegex().Match(title); if (sm.Success) { min = Parse(sm.Groups["min"].Value); max = Parse(sm.Groups["max"].Value); title = SalaryRegex().Replace(title, "").Trim(); }
        var cm = CompanyRegex().Match(title); if (cm.Success) { title = cm.Groups["title"].Value.Trim(); company = cm.Groups["company"].Value.Trim(); }
        return job with { Title = title, Company = company, SalaryMin = min, SalaryMax = max };
    }
    static int? Parse(string s) => int.TryParse(s.Replace("'", "").Replace("’", "").Replace(" ", ""), out var n) ? n : null;
    [GeneratedRegex(
    @"\[CHF\s+(?<min>[\d'’]+)\s*-\s*(?<max>[\d'’]+)\]",
    RegexOptions.IgnoreCase)]
private static partial Regex SalaryRegex();

[GeneratedRegex(
    @"^(?<title>.+?)\s+@\s+(?<company>.+)$")]
private static partial Regex CompanyRegex();

}
