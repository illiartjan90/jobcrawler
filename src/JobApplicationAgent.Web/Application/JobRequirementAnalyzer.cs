using System.Net;
using System.Text.RegularExpressions;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Application;

public enum RequirementImportance
{
    Required,
    Preferred,
    Unknown
}

public enum RequirementCategory
{
    Technology,
    Experience,
    Education,
    Language,
    Domain,
    SoftSkill,
    Other
}

public sealed record JobRequirement(
    string Name,
    RequirementCategory Category,
    RequirementImportance Importance,
    string Evidence);

public sealed class JobRequirementAnalyzer
{
    private static readonly (string Name, string[] Terms)[] Technologies =
    [
        ("C#", ["c#", "csharp"]),
        (".NET", [".net", "dotnet"]),
        ("ASP.NET", ["asp.net", "aspnet"]),
        ("MAUI", ["maui", ".net maui"]),
        ("WPF", ["wpf"]),
        ("PHP", ["php"]),
        ("Symfony", ["symfony"]),
        ("Doctrine", ["doctrine"]),
        ("Laravel", ["laravel"]),
        ("JavaScript", ["javascript"]),
        ("TypeScript", ["typescript"]),
        ("React", ["react"]),
        ("Angular", ["angular"]),
        ("Vue", ["vue", "vue.js", "vuejs"]),
        ("Node.js", ["node.js", "nodejs"]),
        ("HTML", ["html", "html5"]),
        ("CSS", ["css", "css3"]),
        ("TailwindCSS", ["tailwind", "tailwindcss"]),
        ("SQL", ["sql"]),
        ("MySQL", ["mysql"]),
        ("MSSQL", ["mssql", "sql server"]),
        ("PostgreSQL", ["postgresql", "postgres"]),
        ("REST", ["rest api", "restful"]),
        ("GraphQL", ["graphql"]),
        ("Azure", ["azure"]),
        ("AWS", ["aws", "amazon web services"]),
        ("Docker", ["docker"]),
        ("Kubernetes", ["kubernetes", "k8s"]),
        ("Terraform", ["terraform"]),
        ("Java", ["java"]),
        ("Spring", ["spring", "spring boot"]),
        ("Python", ["python"]),
        ("Go", ["golang"]),
        ("Kafka", ["kafka"]),
        ("Redis", ["redis"]),
        ("Git", ["git"]),
        ("CI/CD", ["ci/cd", "continuous integration"]),
        ("Avaloq", ["avaloq"]),
        ("Swift", ["swift"]),
        ("AI", ["artificial intelligence", "künstliche intelligenz"]),
        ("LLM", ["llm", "large language model"])
    ];

    public IReadOnlyList<JobRequirement> Analyze(JobPosting job)
    {
        var html = job.Description ?? string.Empty;
        var result = new List<JobRequirement>();

        ExtractTechnologySection(html, result);
        ExtractRequirementsSection(html, result);

        return Deduplicate(result);
    }

    private static void ExtractTechnologySection(
        string html,
        List<JobRequirement> result)
    {
        var section = ExtractSection(
            html,
            "Technologies:",
            ["More:", "Responsibilities:", "Requirements:"]);

        if (string.IsNullOrWhiteSpace(section))
            return;

        foreach (var item in ExtractListItems(section))
        {
            Add(
                result,
                item,
                RequirementCategory.Technology,
                RequirementImportance.Unknown,
                item);
        }
    }

    private static void ExtractRequirementsSection(
        string html,
        List<JobRequirement> result)
    {
        var section = ExtractSection(
            html,
            "Requirements:",
            ["Responsibilities:", "Technologies:", "More:"]);

        if (string.IsNullOrWhiteSpace(section))
            return;

        foreach (var requirement in ExtractListItems(section))
            AnalyzeRequirement(requirement, result);
    }

    private static void AnalyzeRequirement(
        string text,
        List<JobRequirement> result)
    {
        var lower = text.ToLowerInvariant();

        var importance =
            ContainsPreferredMarker(lower)
                ? RequirementImportance.Preferred
                : RequirementImportance.Required;

        // Technologies
        foreach (var technology in Technologies)
        {
            if (technology.Terms.Any(term =>
                    ContainsTechnology(lower, term)))
            {
                Add(
                    result,
                    technology.Name,
                    RequirementCategory.Technology,
                    importance,
                    text);
            }
        }

        // Database
        if (ContainsAny(
                lower,
                "datenbankkonzept",
                "database concept",
                "datenbankkennt",
                "database knowledge"))
        {
            Add(
                result,
                "Datenbankkonzepte",
                RequirementCategory.Technology,
                importance,
                text);
        }

        // Education
        if (ContainsAny(
                lower,
                "informatikausbildung",
                "it-ausbildung",
                "it ausbildung",
                "ausbildung in informatik",
                "ausbildung im bereich informatik",
                "informatiker efz"))
        {
            Add(
                result,
                "IT-Ausbildung",
                RequirementCategory.Education,
                RequirementImportance.Required,
                text);
        }
        else if (lower.Contains("ausbildung"))
        {
            Add(
                result,
                "Berufsausbildung",
                RequirementCategory.Education,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "fachhochschul",
                "hochschulabschluss",
                "studienabschluss",
                "bachelor",
                "master degree",
                "bachelor degree",
                "university degree"))
        {
            Add(
                result,
                "Hochschul- oder Studienabschluss",
                RequirementCategory.Education,
                ContainsPreferredMarker(lower)
                    ? RequirementImportance.Preferred
                    : RequirementImportance.Required,
                text);
        }

        // Experience
        if (ContainsAny(
                lower,
                "mehrjährige erfahrung",
                "mehrjährige berufserfahrung",
                "mehrjähriger erfahrung",
                "mehrjaehrige erfahrung",
                "several years",
                "multiple years") ||
            Regex.IsMatch(
                lower,
                @"\b[2-9]\+?\s*(?:jahre|jahren|years)\b",
                RegexOptions.IgnoreCase))
        {
            Add(
                result,
                "Mehrjährige relevante Erfahrung",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "requirements engineering",
                "requirement engineering",
                "anforderungsanalyse",
                "anforderungsmanagement"))
        {
            Add(
                result,
                "Requirements Engineering",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(lower, "business analysis", "business analyst"))
        {
            Add(
                result,
                "Business Analysis",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "softwareentwicklung",
                "software development",
                "software engineering",
                "applikationsentwicklung"))
        {
            Add(
                result,
                "Softwareentwicklung",
                RequirementCategory.Experience,
                importance,
                text);
        }

        // Fullstack
        if (ContainsAny(
                lower,
                "fullstack",
                "full-stack",
                "full stack"))
        {
            Add(
                result,
                "Fullstack",
                RequirementCategory.Experience,
                importance,
                text);
        }

        // Sales
        if (ContainsAny(
                lower,
                "verkaufserfahrung",
                "vertriebserfahrung",
                "erfahrung im verkauf",
                "erfahrung im vertrieb",
                "sales experience"))
        {
            Add(
                result,
                "Vertriebserfahrung",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(lower, "verkauf", "vertrieb", "sales"))
        {
            Add(
                result,
                "Verkauf / Vertrieb",
                RequirementCategory.Domain,
                importance,
                text);
        }

        // Consulting
        if (ContainsAny(
                lower,
                "beratungserfahrung",
                "consulting experience",
                "erfahrung in der beratung"))
        {
            Add(
                result,
                "Beratungserfahrung",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "kundenberatung",
                "customer consulting",
                "kunden beraten",
                "kundenbetreuung"))
        {
            Add(
                result,
                "Kundenberatung",
                RequirementCategory.Experience,
                importance,
                text);
        }

        // Support
        if (ContainsAny(
                lower,
                "1st level",
                "first level",
                "first-level",
                "2nd level",
                "second level",
                "second-level",
                "helpdesk",
                "service desk",
                "it-support",
                "it support"))
        {
            Add(
                result,
                "IT-Support",
                RequirementCategory.Experience,
                importance,
                text);
        }

        // Security
        if (ContainsAny(
                lower,
                "cyber security",
                "cybersecurity",
                "informationssicherheit",
                "information security",
                "it security",
                "it-security"))
        {
            Add(
                result,
                "Cyber Security",
                RequirementCategory.Domain,
                importance,
                text);
        }

        // Banking
        if (ContainsAny(
                lower,
                "bankfach",
                "banking experience",
                "bankerfahrung",
                "bankenerfahrung"))
        {
            Add(
                result,
                "Bankfachliche Erfahrung",
                RequirementCategory.Domain,
                importance,
                text);
        }

        // UX/UI
        if (ContainsAny(
                lower,
                "ux/ui",
                "ui/ux",
                "ux design",
                "ui design",
                "user experience design"))
        {
            Add(
                result,
                "UX UI Design",
                RequirementCategory.Experience,
                importance,
                text);
        }

        // Languages
        if (ContainsAny(lower, "deutsch", "german"))
            Add(result, "Deutsch", RequirementCategory.Language, importance, text);

        if (ContainsAny(lower, "englisch", "english"))
            Add(result, "Englisch", RequirementCategory.Language, importance, text);

        if (ContainsAny(lower, "französisch", "franzoesisch", "french"))
            Add(result, "Französisch", RequirementCategory.Language, importance, text);

        if (ContainsAny(lower, "italienisch", "italian"))
            Add(result, "Italienisch", RequirementCategory.Language, importance, text);

        // Soft skills
        if (ContainsAny(lower, "analytisch", "analytical"))
            Add(result, "Analytische Fähigkeiten", RequirementCategory.SoftSkill, importance, text);

        if (ContainsAny(lower, "konzeptionell", "conceptual"))
            Add(result, "Konzeptionelle Fähigkeiten", RequirementCategory.SoftSkill, importance, text);

        if (ContainsAny(
                lower,
                "selbstständig",
                "selbständig",
                "independent working"))
        {
            Add(
                result,
                "Selbstständige Arbeitsweise",
                RequirementCategory.SoftSkill,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "teamfähig",
                "teamfähigkeit",
                "teamplayer",
                "team player",
                "teamarbeit",
                "zusammenarbeit im team"))
        {
            Add(
                result,
                "Teamfähigkeit",
                RequirementCategory.SoftSkill,
                importance,
                text);
        }

        if (ContainsAny(
                lower,
                "kommunikationsstark",
                "kommunikationsfähigkeit",
                "kommunikative",
                "communication skills"))
        {
            Add(
                result,
                "Kommunikationsfähigkeit",
                RequirementCategory.SoftSkill,
                importance,
                text);
        }

        if (ContainsAny(lower, "belastbar", "belastbarkeit", "resilient"))
            Add(result, "Belastbarkeit", RequirementCategory.SoftSkill, importance, text);

        if (ContainsAny(
                lower,
                "lösungsorientiert",
                "loesungsorientiert",
                "solution-oriented",
                "solution oriented"))
        {
            Add(
                result,
                "Lösungsorientierung",
                RequirementCategory.SoftSkill,
                importance,
                text);
        }

        // Project / agile
        if (ContainsAny(
                lower,
                "projektmanagement",
                "project management",
                "projektleitung",
                "project lead"))
        {
            Add(
                result,
                "Projektmanagement",
                RequirementCategory.Experience,
                importance,
                text);
        }

        if (ContainsAny(lower, "scrum", "agile", "kanban"))
        {
            Add(
                result,
                "Agile Arbeitsmethoden",
                RequirementCategory.Experience,
                importance,
                text);
        }
    }

    private static IReadOnlyList<JobRequirement> Deduplicate(
    IEnumerable<JobRequirement> requirements)
{
    return requirements
        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
        .GroupBy(
            x => x.Name.Trim(),
            StringComparer.OrdinalIgnoreCase)
        .Select(group => group
            .OrderBy(x => ImportancePriority(x.Importance))
            .First())
        .ToArray();
}

    private static int ImportancePriority(
        RequirementImportance importance)
    {
        return importance switch
        {
            RequirementImportance.Required => 0,
            RequirementImportance.Preferred => 1,
            RequirementImportance.Unknown => 2,
            _ => 3
        };
    }

    private static void Add(
        List<JobRequirement> result,
        string name,
        RequirementCategory category,
        RequirementImportance importance,
        string evidence)
    {
        result.Add(
            new JobRequirement(
                name,
                category,
                importance,
                evidence));
    }

    private static bool ContainsPreferredMarker(string text)
    {
        return ContainsAny(
            text,
            "idealerweise",
            "von vorteil",
            "wünschenswert",
            "wuenschenswert",
            "nice to have",
            "nice-to-have",
            "optional",
            "preferred",
            "ideally");
    }

    private static bool ContainsAny(
        string text,
        params string[] terms)
    {
        return terms.Any(term =>
            text.Contains(
                term,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTechnology(
        string text,
        string technology)
    {
        if (technology == "c#")
        {
            return text.Contains(
                       "c#",
                       StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(
                       "csharp",
                       StringComparison.OrdinalIgnoreCase);
        }

        if (technology == ".net")
        {
            return Regex.IsMatch(
                text,
                @"(?<![a-z0-9])(?:\.net|dotnet)(?![a-z0-9])",
                RegexOptions.IgnoreCase);
        }

        return Regex.IsMatch(
            text,
            $@"(?<![a-z0-9]){Regex.Escape(technology)}(?![a-z0-9])",
            RegexOptions.IgnoreCase);
    }

    private static string ExtractSection(
        string html,
        string startMarker,
        IReadOnlyCollection<string> endMarkers)
    {
        var start = html.IndexOf(
            startMarker,
            StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            return string.Empty;

        start += startMarker.Length;

        var end = html.Length;

        foreach (var marker in endMarkers)
        {
            var markerIndex = html.IndexOf(
                marker,
                start,
                StringComparison.OrdinalIgnoreCase);

            if (markerIndex >= 0 && markerIndex < end)
                end = markerIndex;
        }

        return html[start..end];
    }

    private static IReadOnlyList<string> ExtractListItems(string html)
    {
        return Regex
            .Matches(
                html,
                @"<li[^>]*>(.*?)</li>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline)
            .Select(match =>
                CleanHtml(match.Groups[1].Value))
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(
            value,
            "<[^>]+>",
            " ");

        var decoded = WebUtility.HtmlDecode(withoutTags);

        return Regex
            .Replace(decoded, @"\s+", " ")
            .Trim();
    }
}