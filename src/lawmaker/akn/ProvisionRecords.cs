#nullable enable

using System;
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    /// <summary>
    /// This class is an abstraction over a <c>Stack</c>, used to keep track of ancestor provisions while parsing a document.
    /// Currently, it only tracks <c>Prov1</c> and <c>SchProv1</c> ancestors. 
    /// The intention is to generalise this to all provisions in future.
    /// </summary>
    /// <remarks>
    /// Note that the current <c>quoteDepth</c> is used throughout this class when there is a need to ignore ancestors 
    /// outside the bounds of the current quoted structure. For example, given the provision hierarchy 
    /// <c>Prov1 > Quoted Structure > Para3</c>, when parsing the <c>Para3</c> element we want 
    /// <c>IsInProv1</c> to evaluate to <c>false</c>.
    /// </remarks>
    class ProvisionRecords
    {
        private Stack<ProvisionRecord> provisionRecords;

        /// <summary>Returns the <c>Type</c> of the current parent provision at the given <paramref name="quoteDepth"/>.</summary>
        /// <param name="quoteDepth">The current quoted structure depth.</param>
        public bool IsInProv1(int quoteDepth) => CurrentType(quoteDepth) == typeof(Prov1);

        public bool IsInSchProv1(int quoteDepth) => CurrentType(quoteDepth) == typeof(SchProv1);

        /// <summary>Returns the <c>Type</c> of the current parent provision at the given <paramref name="quoteDepth"/>.</summary>
        /// <param name="quoteDepth">The quoted structure depth.</param>
        public Type? CurrentType(int quoteDepth) => Peek(quoteDepth)?.Type;

        /// <summary>Returns the <c>Number</c> of the current parent provision at the given <paramref name="quoteDepth"/>.</summary>
        /// <param name="quoteDepth">The current quoted structure depth.</param>
        public IFormattedText? CurrentNumber(int quoteDepth) => Peek(quoteDepth)?.Number;


        public ProvisionRecords()
        {
            provisionRecords = new Stack<ProvisionRecord>();
        }

        /// <summary> Creates a new <c>ProvisionRecord</c> and inserts it at the top of the stack.</summary>
        /// <param name="type">The <c>Type</c> of the provision.</param>
        /// <param name="number">The <c>Number</c> of the provision.</param>
        /// <param name="quoteDepth">The current quoted structure depth.</param>
        public void Push(Type type, IFormattedText number, int quoteDepth)
        {
            provisionRecords.Push(new ProvisionRecord(type, number, quoteDepth));
        }

        /// <summary> Removes and returns the <c>ProvisionRecord</c> at the top of the stack.</summary>
        public ProvisionRecord? Pop()
        {
            if (provisionRecords.Count == 0)
                return null;
            return provisionRecords.Pop();
        }

        /// <summary>
        /// A null-safe wrapper around the <c>Stack.Peek()</c> method which also ignores provision records with 
        /// non-matching <paramref name="quoteDepth"/> values.
        /// </summary>
        /// <param name="quoteDepth">The quoted structure depth.</param>
        /// <returns>
        /// The <c>ProvisionRecord</c> at the top of the stack, presuming it has a matching 
        /// <paramref name="quoteDepth"/> value, otherwise <c>null</c>.
        /// </returns>
        public ProvisionRecord? Peek(int quoteDepth)
        {
            if (provisionRecords.Count == 0)
                return null;
            ProvisionRecord provision = provisionRecords.Peek();
            if (provision.QuoteDepth != quoteDepth)
                return null;
            return provision;
        }

        /// <summary>
        /// A record representing a provision. Stores the <c>Type</c>, <c>Number</c> and <c>quoteDepth</c>.
        /// </summary>
        public record ProvisionRecord(Type Type, IFormattedText Number, int QuoteDepth);

    }

}
