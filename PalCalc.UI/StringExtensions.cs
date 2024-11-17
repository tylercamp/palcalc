using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal static class StringExtensions
    {
        public static string NormalizedPath(this string path) => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        public static bool PathEquals(this string path1, string path2) =>
            (path1 == null && path2 == null) ||
            (path1 != null && path2 != null &&
                path1.NormalizedPath().Equals(path2.NormalizedPath(), StringComparison.InvariantCultureIgnoreCase)
            );

        public static string LimitLength(this string value, int maxLength) =>
            value.Length > maxLength ? value.Substring(0, maxLength - 3) + "..." : value;

        public static string TimeSpanMinutesStr(this TimeSpan ts) => TimeSpan.FromMinutes((int)Math.Round(ts.TotalMinutes)).ToString();
        public static string TimeSpanSecondsStr(this TimeSpan ts) => TimeSpan.FromSeconds((int)Math.Round(ts.TotalSeconds)).ToString();
    }
}
