#nullable enable

using System;
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    class ProvisionRecords
    {
        private Stack<ProvisionRecord> provisionRecords;

        public bool IsInProv1(int quoteDepth) => CurrentType(quoteDepth) == typeof(Prov1);

        public bool IsInSchProv1(int quoteDepth) => CurrentType(quoteDepth) == typeof(SchProv1);

        public Type? CurrentType(int quoteDepth) => Peek(quoteDepth)?.Type;

        public IFormattedText? CurrentNumber(int quoteDepth) => Peek(quoteDepth)?.Number;

        public ProvisionRecords()
        {
            provisionRecords = new Stack<ProvisionRecord>();
        }

        public void Push(Type type, IFormattedText number, int quoteDepth)
        {
            provisionRecords.Push(new ProvisionRecord(type, number, quoteDepth));
        }

        public ProvisionRecord? Pop()
        {
            if (provisionRecords.Count == 0)
                return null;
            return provisionRecords.Pop();
        }

        public ProvisionRecord? Peek(int quoteDepth)
        {
            if (provisionRecords.Count == 0)
                return null;
            ProvisionRecord provision = provisionRecords.Peek();
            if (provision.QuoteDepth != quoteDepth)
                return null;
            return provision;
        }

        public record ProvisionRecord(Type Type, IFormattedText Number, int QuoteDepth);

    }

}
