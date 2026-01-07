
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Razor;
using System.IO.Enumeration;
using System.Reflection.Metadata;
using System.Security.AccessControl;

namespace MediaGateway.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            
            // Add services to the container.

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            var storageUrl = builder.Configuration["Storage:BlobServiceUrl"]; 

            if(string.IsNullOrEmpty(storageUrl))
                throw new InvalidOperationException("Storage:BlobServiceUrl is missing");

            builder.Services.AddSingleton(new BlobServiceClient(new Uri(storageUrl),new DefaultAzureCredential()));

            var app = builder.Build();
                app.UseSwagger();
                app.UseSwaggerUI();

            app.MapGet("/health", () => Results.Ok(new { ok = true }));

            //app.MapGet("/storage/url", (BlobServiceClient client) =>
            //{
            //    return Results.Ok(new { serviceUri = client.Uri.ToString() });

            //});

            app.MapPost("/uploads/init", async (InitUploadRequest req, BlobServiceClient blobServiceClient) =>
            {
                const string containerName = "raw-media";

                var container = blobServiceClient.GetBlobContainerClient(containerName);

                await container.CreateIfNotExistsAsync();

                var safeFile = req.FileName.Replace("\\", "/").Split('/').Last();
                var blobName = $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}-{safeFile}";

                var blobClient = container.GetBlobClient(blobName);

                var expiresOn = DateTimeOffset.UtcNow.AddMinutes(10);

                var delegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
                    DateTimeOffset.UtcNow.AddMinutes(-5), expiresOn
                    );

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                    Resource = "b",
                    ExpiresOn = expiresOn
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

                var sasQuery = sasBuilder.ToSasQueryParameters(delegationKey.Value, blobServiceClient.AccountName);

                var sasUri = new UriBuilder(blobClient.Uri)
                {
                    Query = sasQuery.ToString()
                }.Uri;

                return Results.Ok(new
                {
                    uploadUrl = sasUri.ToString(),
                    blobName,
                    expiresAt = expiresOn
                });
            }); 


            app.UseHttpsRedirection();

            //app.UseAuthorization();

            //app.MapControllers();

            app.Run();
        }

        public record InitUploadRequest(string FileName, string? ContentType);
    }
}
