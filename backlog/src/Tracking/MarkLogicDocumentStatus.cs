#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

namespace Backlog.Tracking;

[Index(nameof(AwsRequestId))]
public class MarkLogicDocumentStatus
{
    public string DocumentUri { get; set; }

    [Key]
    public Guid FakeTreUuid { get; set; }

    public bool Published { get; set; }
    public Guid AwsRequestId { get; set; }
}
