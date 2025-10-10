#nullable enable
using System.Collections.Generic;

namespace UK.Gov.Legislation.Lawmaker;

interface IMetadata
{
    IEnumerable<Reference> GetMetadata();
}
