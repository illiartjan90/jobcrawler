using JobApplicationAgent.Application;

namespace JobApplicationAgent.Tests;

[TestClass]
public sealed class JobRequirementAnalyzerTests
{
    private readonly JobRequirementAnalyzer _analyzer = new();

    [TestMethod]
    public void Analyze_ReturnsEmptyList_WhenDescriptionIsMissing()
    {
        var result = _analyzer.Analyze(TestData.Job(description: null));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Analyze_ReturnsEmptyList_WhenNoKnownSectionsExist()
    {
        var result = _analyzer.Analyze(TestData.Job(description: "<p>Wir freuen uns auf Ihre Bewerbung.</p>"));

        Assert.AreEqual(0, result.Count);
    }
}
