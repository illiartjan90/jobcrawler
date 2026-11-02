using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Tests;

[TestClass]
public sealed class ApplicationStatusesTests
{
    [TestMethod]
    public void All_ContainsEachPipelineStepExactlyOnce()
    {
        Assert.AreEqual(8, ApplicationStatuses.All.Length);
        Assert.AreEqual(ApplicationStatuses.All.Length, ApplicationStatuses.All.Distinct().Count());
    }

    [TestMethod]
    public void All_StartsWithDiscoveredAndKeepsApplyOrder()
    {
        var all = ApplicationStatuses.All.ToList();

        Assert.AreEqual(ApplicationStatuses.Discovered, all[0]);
        Assert.IsTrue(all.IndexOf(ApplicationStatuses.Approved) < all.IndexOf(ApplicationStatuses.Applied),
            "Eine Bewerbung muss freigegeben sein, bevor sie gesendet wird.");
    }
}
