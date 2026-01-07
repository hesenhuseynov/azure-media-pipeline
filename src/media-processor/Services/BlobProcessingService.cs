using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace media_processor.Services;

internal sealed class BlobProcessingService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobProcessingService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

     public async Task CopyRawToProcessAsync( 
          string sourceContainerName,
          string sourceBlobName, 
          string processedContainerName,
          string outputBlobName,
          CancellationToken ct= default
         )
    {
        BlobContainerClient sourceContainer = _blobServiceClient.GetBlobContainerClient(sourceContainerName);

        BlobClient sourceBlob = sourceContainer.GetBlobClient(sourceBlobName);

        BlobContainerClient destContainer = _blobServiceClient.GetBlobContainerClient(processedContainerName);
        await destContainer.CreateIfNotExistsAsync(cancellationToken: ct);

        BlobClient destBlob = destContainer.GetBlobClient(outputBlobName);

        await using Stream src = await sourceBlob.OpenReadAsync(cancellationToken: ct);
        await destBlob.UploadAsync(src, overwrite: true, cancellationToken: ct);
        
    }
}
