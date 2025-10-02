#nullable enable

using System;
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    class ProvisionRecords
    {
        private Stack<ProvisionRecord> provisionRecords;

        public Type? Current => provisionRecords.Count > 0 ? provisionRecords.Peek().Type : null;

        public bool IsInProv1 => Current == typeof(Prov1);

        public bool IsInSchProv1 => Current == typeof(SchProv1);

        public IFormattedText? CurrentNumber => provisionRecords.Count > 0 ? provisionRecords.Peek().Number : null;

        public ProvisionRecords()
        {
            provisionRecords = new Stack<ProvisionRecord>();
        }

        public void Push(Type type, IFormattedText number)
        {
            provisionRecords.Push(new ProvisionRecord(type, number));
        }

        public bool Pop()
        {
            if (provisionRecords.Count == 0)
                return false;
            provisionRecords.Pop();
            return true;
        }

        private record ProvisionRecord(Type Type, IFormattedText Number);

    }

}
