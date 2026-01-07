using Azure.Identity;
using Azure.Storage.Blobs;
using media_processor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);
 
builder.ConfigureFunctionsWebApplication();  

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights(); 

string? storageUrl = Environment.GetEnvironmentVariable("STORAGE_BLOB_SERVICE_URL");

if (string.IsNullOrWhiteSpace(storageUrl))
{
    throw new InvalidOperationException("Missing STORAGE_BLOB_SERVICE_URL app setting"); 
}

builder.Services.AddSingleton(_ =>
    new BlobServiceClient(new Uri(storageUrl), new DefaultAzureCredential()));


builder.Services.AddSingleton<EventGridBlobEventParser>();
builder.Services.AddSingleton<BlobProcessingService>();

await builder.Build().RunAsync();
