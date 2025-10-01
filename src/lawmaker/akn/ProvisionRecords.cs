#nullable enable

using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    class ProvisionRecords
    {
        private Stack<ProvisionRecord> provisionRecords;

        public Type? Current => provisionRecords.Count > 0 ? provisionRecords.Peek().Type : null;

        public string? CurrentNumber => provisionRecords.Count > 0 ? provisionRecords.Peek().Number : null;


        public ProvisionRecords()
        {
            provisionRecords = new Stack<ProvisionRecord>();
        }

        public void Push(Type type, string number)
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

        private record ProvisionRecord(Type Type, string Number);

    }

}
