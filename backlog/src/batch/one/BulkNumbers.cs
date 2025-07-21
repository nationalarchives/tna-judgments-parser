
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;

namespace Backlog.Src.Batch.One
{

    class BulkNumbers
    {

        private static readonly uint LastBeforeThisBatch = uint.Parse(Environment.GetEnvironmentVariable("LAST_BEFORE_BATCH"));

        private static readonly string Path = Environment.GetEnvironmentVariable("BULK_NUMBERS_PATH") ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bulk_numbers.csv");

        internal static uint Next(uint id)
        {
            using var reader = new StreamReader(Path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var lines = csv.GetRecords<Line>().ToList();
            if (lines.Where(line => id.ToString().Equals(line.trib_id)).Any())
                throw new Exception();
            uint next;
            if (lines.Count == 0)
                next = LastBeforeThisBatch + 1;
            else
                next = lines.Select(line => uint.Parse(line.bulk_num)).Max() + 1;
            reader.Close();

            return next;
        }

        /// <summary>
        /// Saves a bulk number assignment for a tribunal ID to the tracking CSV file.
        /// </summary>
        /// <param name="id">The tribunal ID being assigned a bulk number</param>
        /// <param name="bulkNum">The bulk number being assigned</param>
        internal static void Save(uint id, uint bulkNum) {
            using var writer = new StreamWriter(Path, true);
            writer.Write(bulkNum);
            writer.Write(",");
            writer.Write(id);
            writer.WriteLine();
        }

        /// <summary>
        /// Represents a line in the bulk numbers CSV file.
        /// </summary>
        class Line
        {
            /// <summary>
            /// The assigned bulk number
            /// </summary>
            public string bulk_num { get; set; }

            /// <summary>
            /// The tribunal ID that was assigned the bulk number
            /// </summary>
            public string trib_id { get; set; }
        }


    }

}
