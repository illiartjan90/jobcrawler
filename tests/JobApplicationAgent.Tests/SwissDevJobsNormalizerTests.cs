using JobApplicationAgent.Infrastructure;

namespace JobApplicationAgent.Tests;

[TestClass]
public sealed class SwissDevJobsNormalizerTests
{
    private readonly SwissDevJobsNormalizer _normalizer = new();

    [TestMethod]
    public void Normalize_ExtractsSalaryRangeAndCompany_FromTitle()
    {
        var job = TestData.Job(title: "Senior .NET Developer @ Acme AG [CHF 100'000 - 120'000]");

        var result = _normalizer.Normalize(job);

        Assert.AreEqual("Senior .NET Developer", result.Title);
        Assert.AreEqual("Acme AG", result.Company);
        Assert.AreEqual(100000, result.SalaryMin);
        Assert.AreEqual(120000, result.SalaryMax);
    }

    [TestMethod]
    public void Normalize_ExtractsCompany_WhenNoSalaryIsGiven()
    {
        var job = TestData.Job(title: "Fullstack Engineer @ Beispiel GmbH");

        var result = _normalizer.Normalize(job);

        Assert.AreEqual("Fullstack Engineer", result.Title);
        Assert.AreEqual("Beispiel GmbH", result.Company);
        Assert.IsNull(result.SalaryMin);
        Assert.IsNull(result.SalaryMax);
    }

    [TestMethod]
    public void Normalize_KeepsTitle_WhenNoCompanyMarkerExists()
    {
        var job = TestData.Job(title: "  Backend Developer C#  ", company: "Original AG");

        var result = _normalizer.Normalize(job);

        Assert.AreEqual("Backend Developer C#", result.Title);
        Assert.AreEqual("Original AG", result.Company);
    }

    [TestMethod]
    public void Normalize_LeavesJobsFromOtherSourcesUnchanged()
    {
        var job = TestData.Job(title: "Developer @ Acme [CHF 90'000 - 110'000]", source: "Indeed");

        var result = _normalizer.Normalize(job);

        Assert.AreEqual(job, result);
    }
}
