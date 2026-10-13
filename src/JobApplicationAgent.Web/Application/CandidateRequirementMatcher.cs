using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Application;

public enum RequirementMatchStatus
{
    Matched,
    Missing,
    Uncertain
}

public sealed record RequirementMatch(
    string Name,
    RequirementCategory Category,
    RequirementImportance Importance,
    RequirementMatchStatus Status,
    string Reason,
    string Evidence);

public sealed class CandidateRequirementMatcher
{
    public IReadOnlyList<RequirementMatch> Match(
        IReadOnlyList<JobRequirement> requirements,
        CandidateProfile profile)
    {
        return requirements
            .Select(requirement => MatchRequirement(requirement, profile))
            .ToArray();
    }

    private static RequirementMatch MatchRequirement(
        JobRequirement requirement,
        CandidateProfile profile)
    {
        var name = Normalize(requirement.Name);

        // ============================================================
        // SOFT SKILLS
        // ============================================================

        // Soft Skills werden nicht erfunden.
        // Ein CV kann z.B. "teamfähig" behaupten, aber unser
        // strukturiertes Profil liefert dafür aktuell keine belastbare
        // Evidenz.
        if (requirement.Category == RequirementCategory.SoftSkill)
        {
            return Result(
                requirement,
                RequirementMatchStatus.Uncertain,
                "Nicht zuverlässig aus dem strukturierten CV belegbar.");
        }

        // ============================================================
        // SPRACHEN
        // ============================================================

        if (name == "deutsch")
            return MatchLanguage(
                requirement,
                profile,
                "Deutsch",
                "German");

        if (name == "englisch")
            return MatchLanguage(
                requirement,
                profile,
                "Englisch",
                "English");

        if (name is "französisch" or "franzoesisch")
            return MatchLanguage(
                requirement,
                profile,
                "Französisch",
                "Franzoesisch",
                "French");

        if (name == "italienisch")
            return MatchLanguage(
                requirement,
                profile,
                "Italienisch",
                "Italian");

        // ============================================================
        // AUSBILDUNG
        // ============================================================

        if (name.Contains("it-ausbildung") ||
            name.Contains("informatikausbildung") ||
            name.Contains("it- oder bankfachliche ausbildung"))
        {
            var found = HasEducation(
                profile,
                "Informatik",
                "Applikationsentwick",
                "Cyber Security",
                "Cybersecurity",
                "EFZ");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Eine passende IT-Ausbildung ist im Profil belegt."
                    : "Keine passende IT-Ausbildung im Profil belegt.");
        }

        if (name == "berufsausbildung")
        {
            var found = HasEducation(
                profile,
                "EFZ",
                "Informatik",
                "Applikationsentwick");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Eine abgeschlossene Berufsausbildung ist im Profil belegt."
                    : "Keine abgeschlossene Berufsausbildung im Profil belegt.");
        }

        if (name.Contains("hochschul") ||
            name.Contains("studienabschluss") ||
            name.Contains("fachhochschul"))
        {
            return MatchHigherEducation(
                requirement,
                profile);
        }

        // ============================================================
        // DATENBANKEN
        // ============================================================

        if (name is "datenbankkonzepte" or "datenbanken" or "database")
        {
            var found = HasAnySkill(
                profile,
                "SQL",
                "MySQL",
                "MSSQL",
                "PostgreSQL");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "SQL-/Datenbankerfahrung ist im Profil belegt."
                    : "Keine SQL-/Datenbankerfahrung im Profil belegt.");
        }

        // ============================================================
        // API
        // ============================================================

        if (name is "api" or "apis")
        {
            var found =
                HasAnySkill(
                    profile,
                    "REST",
                    "REST API",
                    "GraphQL") ||
                SearchProfile(profile, "API");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "API-Erfahrung ist durch REST/API-Technologien im Profil belegt."
                    : "Keine API-Erfahrung im Profil belegt.");
        }

        // ============================================================
        // BACKEND
        // ============================================================

        if (name is "backend" or "back-end")
        {
            var found = HasAnySkill(
                profile,
                "C#",
                ".NET",
                "ASP.NET",
                "PHP",
                "Symfony",
                "Doctrine",
                "Node.js",
                "Java",
                "Spring",
                "Python",
                "Go");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Backend-Erfahrung ist durch belegte Backend-Technologien im Profil vorhanden."
                    : "Keine Backend-Erfahrung im Profil belegt.");
        }

        // ============================================================
        // FRONTEND
        // ============================================================

        if (name is "frontend" or "front-end")
        {
            var found = HasAnySkill(
                profile,
                "React",
                "TypeScript",
                "JavaScript",
                "HTML",
                "CSS",
                "Vue",
                "Angular");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Frontend-Erfahrung ist durch belegte Frontend-Technologien im Profil vorhanden."
                    : "Keine Frontend-Erfahrung im Profil belegt.");
        }

        // ============================================================
        // FULLSTACK
        // ============================================================

        if (name is "fullstack" or "full-stack" or "full stack")
        {
            var explicitlyMentioned =
                SearchProfile(profile, "Full-Stack") ||
                SearchProfile(profile, "Fullstack") ||
                SearchProfile(profile, "Full Stack");

            var hasBackend = HasAnySkill(
                profile,
                "C#",
                ".NET",
                "ASP.NET",
                "PHP",
                "Symfony",
                "Node.js",
                "Java",
                "Spring");

            var hasFrontend = HasAnySkill(
                profile,
                "React",
                "TypeScript",
                "JavaScript",
                "HTML",
                "CSS",
                "Vue",
                "Angular");

            var found =
                explicitlyMentioned ||
                (hasBackend && hasFrontend);

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Full-Stack-Erfahrung ist durch Frontend- und Backend-Erfahrung im Profil belegt."
                    : "Keine ausreichende Full-Stack-Evidenz im Profil gefunden.");
        }

        // ============================================================
        // SOFTWAREENTWICKLUNG
        // ============================================================

        if (name is "softwareentwicklung" or "software engineering")
        {
            var found =
                HasAnySkill(
                    profile,
                    "C#",
                    ".NET",
                    "PHP",
                    "JavaScript",
                    "TypeScript",
                    "React",
                    "SQL") ||
                SearchProfile(profile, "Developer") ||
                SearchProfile(profile, "Entwickler") ||
                SearchProfile(profile, "Applikationsentwicklung");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Softwareentwicklungserfahrung ist im Profil klar belegt."
                    : "Keine Softwareentwicklungserfahrung im Profil belegt.");
        }

        // ============================================================
        // IT SUPPORT
        // ============================================================

        if (name is "it-support" or "it support" or "support")
        {
            var found =
                SearchProfile(profile, "First-Level") ||
                SearchProfile(profile, "First Level") ||
                SearchProfile(profile, "Second-Level") ||
                SearchProfile(profile, "Second Level") ||
                SearchProfile(profile, "Support");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "IT-Support-Erfahrung ist im Profil belegt."
                    : "Keine IT-Support-Erfahrung im Profil belegt.");
        }

        // ============================================================
        // REQUIREMENTS ENGINEERING
        // ============================================================

        if (name == "requirements engineering")
        {
            var found =
                SearchProfile(profile, "Requirements Engineering") ||
                SearchProfile(profile, "Anforderungsanalyse") ||
                SearchProfile(profile, "Anforderungsmanagement");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Requirements Engineering ist im Profil ausdrücklich belegt."
                    : "Requirements Engineering ist im Profil nicht ausdrücklich belegt.");
        }

        // ============================================================
        // BUSINESS ANALYSIS
        // ============================================================

        if (name == "business analysis")
        {
            var found =
                SearchProfile(profile, "Business Analysis") ||
                SearchProfile(profile, "Business Analyst");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Business Analysis ist im Profil belegt."
                    : "Business Analysis ist im Profil nicht belegt.");
        }

        // ============================================================
        // BANKING / AVALOQ
        // ============================================================

        if (name.Contains("bankfach") ||
            name is "banking" or "bankenerfahrung")
        {
            var found =
                SearchProfile(profile, "Banking") ||
                SearchProfile(profile, "bankfach") ||
                SearchProfile(profile, "Avaloq");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Bankfachliche Erfahrung ist im Profil belegt."
                    : "Keine bankfachliche Erfahrung im Profil belegt.");
        }

        // ============================================================
        // CYBER SECURITY / SECURITY
        // ============================================================

        if (name is
            "cyber security" or
            "cybersecurity" or
            "security" or
            "it security" or
            "it-security" or
            "information security")
        {
            var professionalEvidence =
                SearchExperience(profile, "Cyber Security") ||
                SearchExperience(profile, "Cybersecurity") ||
                SearchExperience(profile, "IT Security") ||
                SearchExperience(profile, "Information Security");

            if (professionalEvidence)
            {
                return Result(
                    requirement,
                    RequirementMatchStatus.Matched,
                    "Berufliche Security-Erfahrung ist im Profil belegt.");
            }

            var educationEvidence = HasEducation(
                profile,
                "Cyber Security",
                "Cybersecurity");

            if (educationEvidence)
            {
                return Result(
                    requirement,
                    RequirementMatchStatus.Uncertain,
                    "Cyber Security ist durch das Studium belegt; berufliche Security-Erfahrung ist daraus jedoch nicht automatisch ableitbar.");
            }

            return Result(
                requirement,
                RequirementMatchStatus.Missing,
                "Keine Security-Erfahrung oder entsprechende Ausbildung im Profil belegt.");
        }

        // ============================================================
        // CLOUD
        // ============================================================

        if (name == "cloud")
        {
            var found =
                HasAnySkill(
                    profile,
                    "AWS",
                    "Azure",
                    "GCP") ||
                SearchProfile(profile, "Cloud");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Cloud-Erfahrung ist im Profil belegt."
                    : "Keine konkrete Cloud-Plattform oder Cloud-Erfahrung im Profil belegt.");
        }

        // ============================================================
        // UX / UI
        // ============================================================

        if (name is
            "ux ui design" or
            "ui ux design" or
            "ux/ui" or
            "ui/ux")
        {
            var found =
                SearchProfile(profile, "UX") ||
                SearchProfile(profile, "UI Design") ||
                SearchProfile(profile, "User Experience");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "UX/UI-Erfahrung ist im Profil ausdrücklich belegt."
                    : "UX/UI-Design wird nicht allein aus HTML/CSS/Frontend-Erfahrung abgeleitet.");
        }

        // ============================================================
        // SALES / VERTRIEB
        // ============================================================

        if (name is
            "vertriebserfahrung" or
            "verkauf / vertrieb" or
            "verkauf" or
            "vertrieb" or
            "sales")
        {
            var found =
                SearchProfile(profile, "Verkauf") ||
                SearchProfile(profile, "Vertrieb") ||
                SearchProfile(profile, "Sales");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Vertriebs-/Verkaufserfahrung ist im Profil belegt."
                    : "Keine Vertriebs-/Verkaufserfahrung im Profil belegt.");
        }

        // ============================================================
        // BERATUNG
        // ============================================================

        if (name is
            "beratungserfahrung" or
            "kundenberatung" or
            "consulting")
        {
            var found =
                SearchProfile(profile, "Beratung") ||
                SearchProfile(profile, "Consulting") ||
                SearchProfile(profile, "Kundenberatung");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Beratungserfahrung ist im Profil ausdrücklich belegt."
                    : "Keine ausdrückliche Beratungserfahrung im Profil belegt.");
        }

        // ============================================================
        // PROJEKTMANAGEMENT
        // ============================================================

        if (name == "projektmanagement")
        {
            var found =
                SearchProfile(profile, "Projektmanagement") ||
                SearchProfile(profile, "Projektleitung") ||
                SearchProfile(profile, "Project Management") ||
                SearchProfile(profile, "Project Lead");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Projektmanagement-Erfahrung ist im Profil belegt."
                    : "Projektmanagement-Erfahrung ist im Profil nicht belegt.");
        }

        // ============================================================
        // AGILE
        // ============================================================

        if (name is
            "agile arbeitsmethoden" or
            "agile" or
            "scrum" or
            "kanban")
        {
            var found =
                SearchProfile(profile, "Agile") ||
                SearchProfile(profile, "Scrum") ||
                SearchProfile(profile, "Kanban");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Agile Arbeitsmethoden sind im Profil belegt."
                    : "Agile Arbeitsmethoden sind im Profil nicht belegt.");
        }

        // ============================================================
        // MEHRJÄHRIGE RELEVANTE ERFAHRUNG
        // ============================================================

        if (name.Contains("mehrjährige relevante erfahrung") ||
            name.Contains("mehrjaehrige relevante erfahrung"))
        {
            return MatchMultiYearExperience(
                requirement,
                profile);
        }

        // ============================================================
        // TECHNOLOGIEN
        // ============================================================

        if (requirement.Category == RequirementCategory.Technology)
        {
            var found = HasSkill(
                profile,
                requirement.Name);

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? $"{requirement.Name} ist im Profil belegt."
                    : $"{requirement.Name} ist im Profil nicht belegt.");
        }

        // ============================================================
        // DOMAIN
        // ============================================================

        if (requirement.Category == RequirementCategory.Domain)
        {
            var found = SearchProfile(
                profile,
                requirement.Name);

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? $"{requirement.Name} ist im Profil ausdrücklich belegt."
                    : $"{requirement.Name} ist im Profil nicht ausdrücklich belegt.");
        }

        // ============================================================
        // FALLBACK
        // ============================================================

        return Result(
            requirement,
            RequirementMatchStatus.Uncertain,
            "Diese Anforderung kann aus dem strukturierten Profil nicht zuverlässig automatisch bewertet werden.");
    }

    // ================================================================
    // MEHRJÄHRIGE ERFAHRUNG
    // ================================================================

    private static RequirementMatch MatchMultiYearExperience(
        JobRequirement requirement,
        CandidateProfile profile)
    {
        var evidence = requirement.Evidence.ToLowerInvariant();

        // Avaloq / Banking
        if (ContainsAny(
                evidence,
                "avaloq",
                "bankfach",
                "banking"))
        {
            var found =
                SearchProfile(profile, "Avaloq") ||
                SearchProfile(profile, "Banking") ||
                SearchProfile(profile, "bankfach");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Mehrjährige einschlägige Banking-/Avaloq-Erfahrung ist im Profil belegt."
                    : "Mehrjährige Berufserfahrung ist vorhanden, aber die verlangte Banking-/Avaloq-Erfahrung ist nicht belegt.");
        }

        // Requirements Engineering
        if (ContainsAny(
                evidence,
                "requirements engineering",
                "anforderungsmanagement",
                "anforderungsanalyse"))
        {
            var found =
                SearchProfile(profile, "Requirements Engineering") ||
                SearchProfile(profile, "Anforderungsmanagement") ||
                SearchProfile(profile, "Anforderungsanalyse");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Mehrjährige einschlägige Requirements-Engineering-Erfahrung ist im Profil belegt."
                    : "Mehrjährige Berufserfahrung ist vorhanden, aber mehrjährige Requirements-Engineering-Erfahrung ist nicht belegt.");
        }

        // Sales
        if (ContainsAny(
                evidence,
                "verkauf",
                "vertrieb",
                "sales"))
        {
            var found =
                SearchProfile(profile, "Verkauf") ||
                SearchProfile(profile, "Vertrieb") ||
                SearchProfile(profile, "Sales");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Missing,
                found
                    ? "Mehrjährige einschlägige Vertriebs-/Verkaufserfahrung ist im Profil belegt."
                    : "Mehrjährige Berufserfahrung ist vorhanden, aber die verlangte Vertriebs-/Verkaufserfahrung ist nicht belegt.");
        }

        // Softwareentwicklung
        if (ContainsAny(
                evidence,
                "softwareentwicklung",
                "software development",
                "software engineering",
                "applikationsentwicklung",
                "developer",
                "entwickler"))
        {
            var found =
                profile.Experience.Count >= 2 &&
                HasAnySkill(
                    profile,
                    "C#",
                    ".NET",
                    "PHP",
                    "JavaScript",
                    "TypeScript");

            return Result(
                requirement,
                found
                    ? RequirementMatchStatus.Matched
                    : RequirementMatchStatus.Uncertain,
                found
                    ? "Mehrjährige Softwareentwicklungserfahrung ist durch mehrere Berufsstationen und Entwicklungstechnologien belegt."
                    : "Berufserfahrung ist vorhanden, die geforderte Dauer der Softwareentwicklungserfahrung ist aber nicht eindeutig ableitbar.");
        }

        // Kein fachlicher Kontext erkannt.
        // Nicht einfach wegen vieler Berufsstationen auf Matched setzen.
        return Result(
            requirement,
            RequirementMatchStatus.Uncertain,
            "Mehrjährige Berufserfahrung ist vorhanden; der fachliche Kontext dieser Anforderung kann jedoch nicht eindeutig automatisch zugeordnet werden.");
    }

    // ================================================================
    // STUDIUM
    // ================================================================

    private static RequirementMatch MatchHigherEducation(
        JobRequirement requirement,
        CandidateProfile profile)
    {
        var education = profile.Education.FirstOrDefault(x =>
            ContainsAny(
                $"{x.Program} {x.School}",
                "Bachelor",
                "BSc",
                "Master",
                "MSc",
                "Studium",
                "Cyber Security"));

        if (education is null)
        {
            return Result(
                requirement,
                RequirementMatchStatus.Missing,
                "Kein entsprechendes Hochschulstudium im Profil belegt.");
        }

        if (LooksCurrentEducation(education.Period))
        {
            return Result(
                requirement,
                RequirementMatchStatus.Uncertain,
                $"Ein passendes Studium ist vorhanden ({education.Program}, {education.Period}), aber noch kein abgeschlossener Studienabschluss belegt.");
        }

        return Result(
            requirement,
            RequirementMatchStatus.Matched,
            $"Ein entsprechender Studienabschluss ist im Profil belegt ({education.Program}).");
    }

    // ================================================================
    // SPRACHEN
    // ================================================================

    private static RequirementMatch MatchLanguage(
        JobRequirement requirement,
        CandidateProfile profile,
        params string[] aliases)
    {
        var found = profile.Languages.Any(language =>
            aliases.Any(alias =>
                language.Contains(
                    alias,
                    StringComparison.OrdinalIgnoreCase)));

        return Result(
            requirement,
            found
                ? RequirementMatchStatus.Matched
                : RequirementMatchStatus.Missing,
            found
                ? $"{requirement.Name} ist im Kandidatenprofil belegt."
                : $"{requirement.Name} ist im Kandidatenprofil nicht belegt.");
    }

    // ================================================================
    // RESULT
    // ================================================================

    private static RequirementMatch Result(
        JobRequirement requirement,
        RequirementMatchStatus status,
        string reason)
    {
        return new RequirementMatch(
            requirement.Name,
            requirement.Category,
            requirement.Importance,
            status,
            reason,
            requirement.Evidence);
    }

    // ================================================================
    // AUSBILDUNG
    // ================================================================

    private static bool HasEducation(
        CandidateProfile profile,
        params string[] terms)
    {
        return profile.Education.Any(education =>
        {
            var text =
                $"{education.Program} {education.School}";

            return terms.Any(term =>
                text.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase));
        });
    }

    private static bool LooksCurrentEducation(string period)
    {
        return ContainsAny(
            period,
            "heute",
            "present",
            "aktuell",
            "current");
    }

    // ================================================================
    // SKILLS
    // ================================================================

    private static bool HasAnySkill(
        CandidateProfile profile,
        params string[] skills)
    {
        return skills.Any(skill =>
            HasSkill(profile, skill));
    }

    private static bool HasSkill(
        CandidateProfile profile,
        string requirement)
    {
        if (profile.Skills.Any(candidateSkill =>
                Equivalent(
                    candidateSkill,
                    requirement)))
        {
            return true;
        }

        return profile.Experience.Any(experience =>
            experience.Skills.Any(candidateSkill =>
                Equivalent(
                    candidateSkill,
                    requirement)));
    }

    private static bool Equivalent(
        string candidate,
        string requirement)
    {
        var c = Normalize(candidate);
        var r = Normalize(requirement);

        if (c == r)
            return true;

        // SQL ist ein Oberbegriff.
        if (r == "sql")
        {
            return c is
                "sql" or
                "mysql" or
                "mssql" or
                "postgresql" or
                "sqlserver";
        }

        // .NET
        if (r == ".net")
        {
            return c is
                ".net" or
                "dotnet" or
                "asp.net" or
                "aspnet";
        }

        if (r == "asp.net")
        {
            return c is
                "asp.net" or
                "aspnet" or
                ".net" or
                "dotnet";
        }

        // JavaScript
        if (r == "javascript")
        {
            return c is
                "javascript" or
                "js";
        }

        // TypeScript
        if (r == "typescript")
        {
            return c is
                "typescript" or
                "ts";
        }

        // C#
        if (r == "c#")
        {
            return c is
                "c#" or
                "csharp";
        }

        // Node
        if (r == "node.js")
        {
            return c is
                "node.js" or
                "nodejs";
        }

        // MSSQL
        if (r == "mssql")
        {
            return c is
                "mssql" or
                "sqlserver";
        }

        // HTML
        if (r == "html")
        {
            return c is
                "html" or
                "html5";
        }

        // CSS
        if (r == "css")
        {
            return c is
                "css" or
                "css3";
        }

        // REST
        if (r == "rest")
        {
            return c is
                "rest" or
                "restapi" or
                "restful";
        }

        // Vue
        if (r == "vue")
        {
            return c is
                "vue" or
                "vue.js" or
                "vuejs";
        }

        return false;
    }

    // ================================================================
    // PROFIL-SUCHE
    // ================================================================

    private static bool SearchProfile(
        CandidateProfile profile,
        string term)
    {
        if (profile.Headline.Contains(
                term,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (profile.Skills.Any(skill =>
                skill.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (profile.Education.Any(education =>
                education.Program.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase) ||
                education.School.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return SearchExperience(
            profile,
            term);
    }

    private static bool SearchExperience(
        CandidateProfile profile,
        string term)
    {
        return profile.Experience.Any(experience =>
            experience.Role.Contains(
                term,
                StringComparison.OrdinalIgnoreCase) ||

            experience.Summary.Contains(
                term,
                StringComparison.OrdinalIgnoreCase) ||

            experience.Skills.Any(skill =>
                skill.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase)));
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private static bool ContainsAny(
        string text,
        params string[] terms)
    {
        return terms.Any(term =>
            text.Contains(
                term,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("full stack", "fullstack")
            .Replace("full-stack", "fullstack")
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }
}