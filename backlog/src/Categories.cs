
using System.Collections.Generic;

namespace Backlog.Src
{

    class Categories
    {
        private static readonly Dictionary<int, (string Subcategory, string Category)> _ = new() {
            { 1, ("Refusal to register", "Appeals") },
            { 2, ("Withdrawal of exemption", "Appeals") },
            { 3, ("Limited registration", "Appeals") },
            { 4, ("Refusal to continue registration", "Appeals") },
            { 5, ("Registration variation", "Appeals") },
            { 6, ("Recording of complaint decision", "Appeals") },
            { 7, ("Breach of code of standards", "Disciplinary Charges") },
            { 8, ("Breach of rules", "Disciplinary Charges") },
        };

        /// <param name="code">the code of the Category/Subcategory pair</param>
        /// <exception cref="KeyNotFoundException">if code is not known</exception>
        internal static (string Subcategory, string Category) Get(int code)
        {
            return _[code];
        }

    }

}
