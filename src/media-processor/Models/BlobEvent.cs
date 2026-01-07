using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace media_processor.Models;

internal sealed record BlobEvent(
     string EventId,
     string BlobUrl,
     string Subject,
     string Container,
     string BlobName
    );

