using GraphSharp.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model.CSV
{
    internal class CSVExporter<T>(List<CSVPropertySerializer<T>> serializers)
    {
        public string Export(IEnumerable<T> items)
        {
            var serializerReservations = new List<(int, CSVPropertySerializer<T>)>();
            var headerParts = new List<string>();

            foreach (var s in serializers)
            {
                var cols = s.ColumnsReservations(items);
                serializerReservations.Add((cols.Count, s));

                headerParts.AddRange(cols);
            }

            // don't just use ',' - regions use ';' instead
            var separator = CultureInfo.CurrentUICulture.TextInfo.ListSeparator.Trim();
            var lines = new List<string>
            {
                string.Join(separator, headerParts)
            };

            foreach (var item in items)
            {
                var itemParts = new List<string>();

                foreach (var sr in serializerReservations)
                {
                    var (cols, s) = sr;

                    var sParts = s.ValuesOf(item);

                    itemParts.AddRange([
                        .. sParts.Select(p => $"\"{p.Replace("\"", "\"\"")}\""),
                        .. Enumerable.Repeat("", cols - sParts.Count)
                    ]);
                }

                lines.Add(string.Join(separator, itemParts));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
