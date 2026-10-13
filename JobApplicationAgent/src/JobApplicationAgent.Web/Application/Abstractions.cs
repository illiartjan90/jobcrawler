using JobApplicationAgent.Domain;
namespace JobApplicationAgent.Application;

public interface IJobSource { string Name { get; } Task<IReadOnlyCollection<JobPosting>> SearchAsync(CancellationToken ct=default); }
public interface IJobNormalizer { JobPosting Normalize(JobPosting job); }
public interface IJobRepository {
 Task UpsertAsync(JobPosting job, JobMatch match, CancellationToken ct=default);
 Task<IReadOnlyList<JobView>> ListAsync(string? q=null,int minScore=0,string? status=null,CancellationToken ct=default);
 Task<JobView?> GetAsync(string id,CancellationToken ct=default);
 Task SetStatusAsync(string id,string status,CancellationToken ct=default);
 Task<Dictionary<string,int>> StatsAsync(CancellationToken ct=default);
}
public interface IJobMatcher { JobMatch Match(JobPosting job, CandidateProfile profile); }
public interface IDocumentService { Task<(string FileName,byte[] Bytes,string ContentType)> CreateCvAsync(JobView job,CandidateProfile profile,string format="docx"); Task<(string FileName,byte[] Bytes,string ContentType)> CreateCoverLetterAsync(JobView job,CandidateProfile profile,string format="docx"); }
public interface IAiService { bool IsEnabled { get; } Task<string> GenerateAsync(string instruction,string input,CancellationToken ct=default); }
