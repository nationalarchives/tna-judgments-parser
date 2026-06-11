#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

namespace Backlog.Tracking;

[Index(nameof(NcnReference))]
[Index(nameof(TreReference))]
public class CloudwatchSummaryLogLine
{
    [Key]
    public Guid RequestId { get; set; }

    public string MarkLogicUri { get; set; }
    public Guid TreReference { get; set; }
    public string NcnReference { get; set; }
    public string LastInfoMessage { get; set; }
    public string LastWarningMessage { get; set; }
    public string LastErrorMessage { get; set; }
    public string LambdaReport { get; set; }
}
