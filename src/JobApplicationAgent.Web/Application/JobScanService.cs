using JobApplicationAgent.Domain;
namespace JobApplicationAgent.Application;
public sealed class JobScanService(IEnumerable<IJobSource> sources,IEnumerable<IJobNormalizer> normalizers,IJobMatcher matcher,IJobRepository repo,CandidateProfile profile,ILogger<JobScanService> log) {
 public async Task<int> ScanAsync(CancellationToken ct=default) {
  var count=0;
  foreach(var source in sources) {
   try {
    var jobs=await source.SearchAsync(ct);
    foreach(var raw in jobs) {
     var job=normalizers.Aggregate(raw,(j,n)=>n.Normalize(j));
     await repo.UpsertAsync(job,matcher.Match(job,profile),ct); count++;
    }
   } catch(Exception ex) { log.LogError(ex,"Job source {Source} failed",source.Name); }
  }
  return count;
 }
}
