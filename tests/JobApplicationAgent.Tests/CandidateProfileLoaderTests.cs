using JobApplicationAgent.Application;

namespace JobApplicationAgent.Tests;

[TestClass]
public sealed class CandidateProfileLoaderTests
{
    private string _path = string.Empty;

    [TestInitialize]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"profile-{Guid.NewGuid():N}.json");

    [TestCleanup]
    public void TearDown()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [TestMethod]
    public void Load_ReadsValidProfile()
    {
        File.WriteAllText(_path, """
        {
          "name": "Max Muster",
          "headline": "Software Engineer",
          "skills": ["C#", ".NET"],
          "languages": ["Deutsch"],
          "experience": [],
          "education": []
        }
        """);

        var profile = CandidateProfileLoader.Load(_path);

        Assert.AreEqual("Max Muster", profile.Name);
        Assert.AreEqual(2, profile.Skills.Count);
        CollectionAssert.Contains(profile.Skills.ToList(), "C#");
    }

    [TestMethod]
    public void Load_ThrowsFileNotFound_WhenFileIsMissing()
    {
        Assert.ThrowsException<FileNotFoundException>(() => CandidateProfileLoader.Load(_path));
    }

    [TestMethod]
    public void Load_ThrowsInvalidOperation_WhenNameIsEmpty()
    {
        File.WriteAllText(_path, """
        { "name": " ", "headline": "Dev", "skills": [], "languages": [], "experience": [], "education": [] }
        """);

        Assert.ThrowsException<InvalidOperationException>(() => CandidateProfileLoader.Load(_path));
    }

    [TestMethod]
    public void Load_ThrowsInvalidOperation_WhenSkillsAreMissing()
    {
        File.WriteAllText(_path, """
        { "name": "Max", "headline": "Dev", "languages": [], "experience": [], "education": [] }
        """);

        Assert.ThrowsException<InvalidOperationException>(() => CandidateProfileLoader.Load(_path));
    }
}
