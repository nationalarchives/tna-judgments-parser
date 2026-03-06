
#nullable enable

using UK.Gov.Legislation.Lawmaker.Headers;

namespace UK.Gov.Legislation.Lawmaker;

interface IHeaderVisitor {
    NIHeader? VisitNI(NIHeader? niHeader);
    UKHeader? VisitUK(UKHeader? ukHeader);
    SPHeader? VisitSP(SPHeader? spHeader);
    UKHeader? VisitSC(UKHeader? scHeader); // SC re-use UKHeader
    CMHeader? VisitCM(CMHeader? cmHeader);

}
