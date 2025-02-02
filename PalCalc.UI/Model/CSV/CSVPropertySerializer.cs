using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model.CSV
{
    internal interface CSVPropertySerializer<T>
    {
        List<string> ColumnsReservations(IEnumerable<T> items);

        List<string> ValuesOf(T item);
    }

    public class SimpleCSVPropertySerializer<T>(string columnName, Func<T, string> selector) : CSVPropertySerializer<T>
    {
        public List<string> ColumnsReservations(IEnumerable<T> items) => [columnName];

        public List<string> ValuesOf(T item) => [selector(item)];
    }
}
