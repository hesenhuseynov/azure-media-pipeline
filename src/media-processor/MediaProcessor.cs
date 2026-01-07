
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Text;
using System.Text.Unicode;
using Azure.Core;
using Azure.Messaging;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs.Models;
using media_processor.Models;
using media_processor.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace Company.Function;



internal sealed class MediaProcessor
{
    //private const string RawContainer = "raw-media";
    //private const string ProcessedContainer = "processed-media";


    private readonly ILogger<MediaProcessor> _logger;
    private readonly EventGridBlobEventParser _parser;
    private readonly BlobProcessingService _blobService;


    public MediaProcessor(ILogger<MediaProcessor> logger, EventGridBlobEventParser parser, BlobProcessingService blobService)
    {
        _logger = logger;
        _parser = parser;
        _blobService = blobService;
    }

    [Function(nameof(MediaProcessor))]
    public async Task Run(
       [ServiceBusTrigger("q-media-processing", Connection = "sbmediadev_SERVICEBUS")]
        ServiceBusReceivedMessage message,
       ServiceBusMessageActions messageActions)
    {
        string body = Encoding.UTF8.GetString(message.Body.ToArray()).Trim();

        _logger.LogInformation(
               "RECV MessageId={MessageId} DeliveryCount={DeliveryCount} ContentType={ContentType} BodyLen={Len}",
               message.MessageId, message.DeliveryCount, message.ContentType, body.Length);


        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("Skip: empty body,MessageId= {MessageId}", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }


        if (!(body.StartsWith('{') || body.StartsWith('[')))
        {
            _logger.LogWarning("NOT JSON,MessageId={MessageId}", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }


        BlobEvent ev;

        try
        {
            ev = _parser.Parse(body);
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse failed.DLQ.MessageId = {MessageId}", message.MessageId);

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "Parse Failed",
                deadLetterErrorDescription: ex.Message
                );

            return;
        }

        if (!string.Equals(ev.Container, "raw-media", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Skip :container is {Container}. Subject={Subject}", ev.Container, ev.Subject);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        string outputBlobName = "processed/" + ev.BlobName.Replace("\\", "/").TrimStart('/');

        try
        {
            await _blobService.CopyRawToProcessAsync(
             sourceContainerName: ev.Container,
             sourceBlobName: ev.BlobName,
             processedContainerName: "processed-media",
             outputBlobName: outputBlobName);
            await messageActions.CompleteMessageAsync(message);
        }


        catch (Azure.RequestFailedException ex) when (ex.Status is 401 or 403)
        {

            _logger.LogError(ex, "Auth/RBAC failed.DLQ. MessageId={MessageId}", message.MessageId);

            await messageActions.DeadLetterMessageAsync(
               message,
              deadLetterReason: "StorageAuthFailed",
              deadLetterErrorDescription: ex.Message

            );
            return;
        }

        catch(Azure.RequestFailedException ex) when(ex.Status is 402  or 403)
        {
            _logger.LogError(ex, "Auth/RBAC failed.DLQ. MessageId={MessageId}", message.MessageId);
             
        }

        catch (Exception ex)
        {
            _logger.LogError(
        ex,
        "Processing failed. Throw for retry. MessageId={MessageId} EventId={EventId} Output={Output}",
        message.MessageId,
        ev.EventId,
        outputBlobName
    );

            throw new InvalidOperationException(
                $"Processing failed. MessageId={message.MessageId}, EventId={ev.EventId}, Output={outputBlobName}",
                ex
            );
        }
        _logger.LogInformation("DONE. Output={Output} EventId={EventId}", outputBlobName, ev.EventId);

    }
}
