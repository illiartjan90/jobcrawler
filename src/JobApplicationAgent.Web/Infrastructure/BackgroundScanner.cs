using JobApplicationAgent.Application;
namespace JobApplicationAgent.Infrastructure;
public sealed class BackgroundScanner(IServiceProvider services,IConfiguration cfg,ILogger<BackgroundScanner> log):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){var mins=Math.Max(30,cfg.GetValue<int?>("JobSearch:IntervalMinutes")??180);if(cfg.GetValue<bool>("JobSearch:RunOnStartup"))await Run(stoppingToken);using var timer=new PeriodicTimer(TimeSpan.FromMinutes(mins));while(await timer.WaitForNextTickAsync(stoppingToken))await Run(stoppingToken);}
 async Task Run(CancellationToken ct){try{using var scope=services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<JobScanService>();var n=await s.ScanAsync(ct);log.LogInformation("Background scan imported {Count} jobs",n);}catch(Exception ex){log.LogError(ex,"Background scan failed");}}
}
