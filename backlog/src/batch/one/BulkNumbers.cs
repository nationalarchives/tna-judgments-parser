
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

        private static readonly uint LastBeforeThisBatch = 357;

        private static readonly string Path = @"C:\Users\Administrator\TDR-2024-CG6F_converted\bulk_numbers.csv";

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

            // using var stream = new FileStream(path, FileMode.Append, FileAccess.Write);
            // using var writer = new StreamWriter(stream);
            // using var csv2 = new CsvWriter(writer, CultureInfo.InvariantCulture);
            // Line line = new() { bulk_num = next.ToString(), trib_id = id.ToString() };
            // csv2.WriteRecords([line]);

            return next;
        }

        internal static void Save(uint id, uint bulkNum) {
            // using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write);
            using var writer = new StreamWriter(Path, true);
            // using var csv2 = new CsvWriter(writer, CultureInfo.InvariantCulture);
            // Line line = new() { bulk_num = bulkNum.ToString(), trib_id = id.ToString() };
            // csv2.WriteRecords([line]);
            writer.Write(bulkNum);
            writer.Write(",");
            writer.Write(id);
            writer.WriteLine();
        }

        class Line
        {

            public string bulk_num { get; set; }
            public string trib_id { get; set; }

        }


    }

}
