using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using media_processor.Models;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace media_processor.Services;

internal sealed  class EventGridBlobEventParser
{

    public BlobEvent Parse(string body)
    { 
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentNullException(nameof(body), "Message body is empty");
        }

        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
       
        JsonElement ev = root.ValueKind == JsonValueKind.Array ? root[0] : root;

        string? eventId = TryGetString(ev, "id") ?? Guid.NewGuid().ToString("N");

        string subject = TryGetString(ev, "subject") ?? string.Empty;

        string?  blobUrl = ExtractBlobUrl(ev);

        bool ok = TryParseSubject(subject, out  string  container, out  string  blobName);

        if (!ok)
        {
            ok = TryParseSubject(blobUrl, out container, out blobName);  
        }

        if (!ok)
        {
            throw new InvalidOperationException(
                $"Could not parse container/blobName from subject or url. subject='{subject}', url='{blobUrl}'");
        }

        return new BlobEvent(
        EventId: eventId,
        BlobUrl: blobUrl,
        Subject: subject,
        Container: container,
        BlobName: blobName
    );

    }

    public static string ExtractBlobUrl(JsonElement eventGridEvent)
    {
        if(!eventGridEvent.TryGetProperty("data", out JsonElement data))
        {
            throw new InvalidOperationException("Missing 'data' in Event Grid payload.");
        }

        string  url = TryGetString(data, "url") ?? TryGetString(data, "blobUrl");

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Missing 'data url' int the Event Grid payload "); 
        }
        return url;
    } 

    private static string? TryGetString(JsonElement  element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement p)
            && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
    
    public static bool TryParseSubject(string subject, out string container,out string blobName)
    {
        container = "";
        blobName = "";

        const string containersMarker = "/containers/";
        int containersIndex = subject.IndexOf(containersMarker, StringComparison.OrdinalIgnoreCase);

        if (containersIndex < 0)
        {
            return false;
        }

        string afterContainers= subject[(containersIndex + containersMarker.Length)..];

        const string blobsMarker = "/blobs/";

        int blobsIndex = afterContainers.IndexOf(blobsMarker, StringComparison.OrdinalIgnoreCase);
        if (blobsIndex < 0)
        {
            return false; 
        }

        container = afterContainers[..blobsIndex];

        blobName = afterContainers[(blobsIndex + blobsMarker.Length)..];

        if(string.IsNullOrWhiteSpace(container) || string.IsNullOrWhiteSpace(blobName))
        {
            return false;
        }

        return true;
    }
 
}
