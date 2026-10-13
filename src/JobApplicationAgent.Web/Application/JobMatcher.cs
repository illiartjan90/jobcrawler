using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Application;

public sealed class JobMatcher(
    JobRequirementAnalyzer analyzer,
    CandidateRequirementMatcher requirementMatcher)
    : IJobMatcher
{
    public JobMatch Match(
        JobPosting job,
        CandidateProfile profile)
    {
        var requirements = analyzer.Analyze(job);

        var matches = requirementMatcher.Match(
            requirements,
            profile);

        if (matches.Count == 0)
        {
            return new JobMatch(
                25,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "Keine strukturierten Anforderungen erkannt. " +
                "Der Match muss manuell geprüft werden.");
        }

        var matched = matches
            .Where(x =>
                x.Status == RequirementMatchStatus.Matched)
            .ToArray();

        var missing = matches
            .Where(x =>
                x.Status == RequirementMatchStatus.Missing)
            .ToArray();

        var uncertain = matches
            .Where(x =>
                x.Status == RequirementMatchStatus.Uncertain)
            .ToArray();

        var required = matches
            .Where(x =>
                x.Importance == RequirementImportance.Required)
            .ToArray();

        var preferred = matches
            .Where(x =>
                x.Importance == RequirementImportance.Preferred)
            .ToArray();

        var unknownImportance = matches
            .Where(x =>
                x.Importance == RequirementImportance.Unknown)
            .ToArray();


        // ------------------------------------------------------------
        // Gewichtung
        //
        // Required  = 75 %
        // Preferred = 15 %
        // Unknown   = 10 %
        //
        // "Uncertain" bekommt Teilpunkte, damit fehlende Evidenz
        // nicht automatisch als definitiv fehlende Fähigkeit gilt.
        // ------------------------------------------------------------

        var requiredScore = CalculateGroupScore(
            required,
            75);

        var preferredScore = CalculateGroupScore(
            preferred,
            15);

        var unknownScore = CalculateGroupScore(
            unknownImportance,
            10);


        var score =
            requiredScore +
            preferredScore +
            unknownScore;


        // ------------------------------------------------------------
        // Wenn eine Kategorie gar nicht vorkommt, sollen deren Punkte
        // nicht einfach verloren gehen.
        //
        // Deshalb normalisieren wir anhand der tatsächlich vorhandenen
        // Kategorien auf 100.
        // ------------------------------------------------------------

        var availableWeight = 0;

        if (required.Length > 0)
            availableWeight += 75;

        if (preferred.Length > 0)
            availableWeight += 15;

        if (unknownImportance.Length > 0)
            availableWeight += 10;

        if (availableWeight > 0 &&
            availableWeight < 100)
        {
            score = (int)Math.Round(
                score * 100.0 /
                availableWeight);
        }


        // ------------------------------------------------------------
        // Harte Bremse:
        //
        // Wenn mindestens eine REQUIRED-Anforderung fehlt,
        // soll der Job nicht wie ein nahezu perfekter Match aussehen.
        // ------------------------------------------------------------

        var missingRequired = required.Count(x =>
            x.Status == RequirementMatchStatus.Missing);

        if (missingRequired >= 3)
        {
            score = Math.Min(score, 55);
        }
        else if (missingRequired == 2)
        {
            score = Math.Min(score, 65);
        }
        else if (missingRequired == 1)
        {
            score = Math.Min(score, 78);
        }


        score = Math.Clamp(
            score,
            0,
            100);


        var matchingSkills = matched
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingSkills = missing
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();


        var explanation = BuildExplanation(
            matches,
            required,
            preferred,
            unknownImportance,
            matched,
            missing,
            uncertain,
            requiredScore,
            preferredScore,
            unknownScore,
            score);


        return new JobMatch(
            score,
            matchingSkills,
            missingSkills,
            explanation);
    }


    // ================================================================
    // SCORING
    // ================================================================

    private static int CalculateGroupScore(
        IReadOnlyCollection<RequirementMatch> requirements,
        int maximumPoints)
    {
        if (requirements.Count == 0)
            return 0;

        double earned = 0;

        foreach (var requirement in requirements)
        {
            earned += requirement.Status switch
            {
                RequirementMatchStatus.Matched => 1.0,

                // Unklar = Teilpunkte.
                // Wir behaupten damit weder erfüllt noch nicht erfüllt.
                RequirementMatchStatus.Uncertain => 0.35,

                RequirementMatchStatus.Missing => 0.0,

                _ => 0.0
            };
        }

        var ratio =
            earned /
            requirements.Count;

        return (int)Math.Round(
            ratio * maximumPoints);
    }


    // ================================================================
    // EXPLANATION
    // ================================================================

    private static string BuildExplanation(
        IReadOnlyCollection<RequirementMatch> all,
        IReadOnlyCollection<RequirementMatch> required,
        IReadOnlyCollection<RequirementMatch> preferred,
        IReadOnlyCollection<RequirementMatch> unknownImportance,
        IReadOnlyCollection<RequirementMatch> matched,
        IReadOnlyCollection<RequirementMatch> missing,
        IReadOnlyCollection<RequirementMatch> uncertain,
        int requiredScore,
        int preferredScore,
        int unknownScore,
        int finalScore)
    {
        var requiredMatched = required.Count(x =>
            x.Status == RequirementMatchStatus.Matched);

        var requiredMissing = required.Count(x =>
            x.Status == RequirementMatchStatus.Missing);

        var requiredUncertain = required.Count(x =>
            x.Status == RequirementMatchStatus.Uncertain);

        return
            $"{matched.Count} von {all.Count} erkannten Anforderungen " +
            $"sind im Profil belegt; " +
            $"{missing.Count} sind nicht belegt; " +
            $"{uncertain.Count} müssen manuell geprüft werden. " +

            $"Pflichtanforderungen: " +
            $"{requiredMatched} belegt, " +
            $"{requiredMissing} nicht belegt, " +
            $"{requiredUncertain} unklar. " +

            $"Bewertung: Required {requiredScore}/75, " +
            $"Preferred {preferredScore}/15, " +
            $"weitere Anforderungen {unknownScore}/10. " +
            $"Gesamtscore {finalScore}%.";
    }
}