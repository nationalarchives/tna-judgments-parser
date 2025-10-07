
using System.Collections.Generic;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Roman
    {

        private static readonly Dictionary<char, int> LowerMap = new()
            {
                {'i', 1}, {'v', 5}, {'x', 10}, {'l', 50},
                {'c', 100}, {'d', 500}, {'m', 1000}
            };

        public static int LowerRomanToInt(string s)
        {
            int total = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int value = LowerMap[s[i]];
                if (i + 1 < s.Length && LowerMap[s[i]] < LowerMap[s[i + 1]])
                    total -= value;
                else
                    total += value;
            }
            return total;
        }

    }

}
