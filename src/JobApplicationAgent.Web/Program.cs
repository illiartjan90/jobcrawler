using JobApplicationAgent.Application;
using JobApplicationAgent.Infrastructure;
using JobApplicationAgent.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobAgent(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

await app.Services.InitializeJobAgentAsync();

app.MapJobApplicationEndpoints();

app.Run();