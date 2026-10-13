using System.Text.Json;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Application;

public static class CandidateProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CandidateProfile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Candidate profile was not found: {path}",
                path);
        }

        var json = File.ReadAllText(path);

        var profile = JsonSerializer.Deserialize<CandidateProfile>(
            json,
            JsonOptions);

        if (profile is null)
        {
            throw new InvalidOperationException(
                $"Candidate profile could not be deserialized: {path}");
        }

        Validate(profile);

        return profile;
    }

    private static void Validate(CandidateProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new InvalidOperationException(
                "Candidate profile requires a name.");

        if (string.IsNullOrWhiteSpace(profile.Headline))
            throw new InvalidOperationException(
                "Candidate profile requires a headline.");

        if (profile.Skills is null)
            throw new InvalidOperationException(
                "Candidate profile requires a skills collection.");

        if (profile.Languages is null)
            throw new InvalidOperationException(
                "Candidate profile requires a languages collection.");

        if (profile.Experience is null)
            throw new InvalidOperationException(
                "Candidate profile requires an experience collection.");

        if (profile.Education is null)
            throw new InvalidOperationException(
                "Candidate profile requires an education collection.");
    }
}