#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace Backlog.Tracking;

internal class ParserControl
{
    [Key]
    public Guid SourceUuid { get; set; }
    public TrackerStatus TrackerStatus { get; set; }
}
