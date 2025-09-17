#nullable enable
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;

interface ILineable
{
    IEnumerable<WLine> Lines { get; }
    WLine? GetLastLine() => Lines.LastOrDefault();

}