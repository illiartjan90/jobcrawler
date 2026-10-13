using System.ServiceModel.Syndication;
using System.Xml;
using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;
namespace JobApplicationAgent.Infrastructure;
public sealed class SwissDevJobsSource(HttpClient http):IJobSource {
 public string Name=>"SwissDevJobs";
 public async Task<IReadOnlyCollection<JobPosting>> SearchAsync(CancellationToken ct=default) {
  await using var stream=await http.GetStreamAsync("https://swissdevjobs.ch/rss",ct);
  using var reader=XmlReader.Create(stream); var feed=SyndicationFeed.Load(reader); var jobs=new List<JobPosting>();
  foreach(var item in feed.Items) {
   var url=item.Links.FirstOrDefault()?.Uri?.ToString(); if(string.IsNullOrWhiteSpace(url)) continue;
   jobs.Add(new(string.IsNullOrWhiteSpace(item.Id)?url:item.Id,item.Title?.Text??"Unbekannte Stelle",item.Authors.FirstOrDefault()?.Name??"Unbekannt",null,item.Summary?.Text,null,null,"CHF",Name,url,item.PublishDate==default?null:item.PublishDate,DateTimeOffset.UtcNow));
  }
  return jobs;
 }
}
