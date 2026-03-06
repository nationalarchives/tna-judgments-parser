#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

interface IHeader {
    IHeader? Visit(IHeaderVisitor visitor, HeaderVisitorContext Context);
}
