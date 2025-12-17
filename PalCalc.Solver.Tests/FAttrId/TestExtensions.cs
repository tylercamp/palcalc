using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    internal static class TestExtensions
    {
        public static int IndexOf<T>(this IEnumerable<T> e, T v) =>
            e.ZipWithIndex().First(t => EqualityComparer<T>.Default.Equals(t.Item1, v)).Item2;
    }
}
