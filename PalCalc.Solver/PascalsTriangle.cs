using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    // N choose K -> look up column K at row N of pascal's triangle
    public class PascalsTriangle
    {
        public PascalsTriangle(int numRows)
        {
            AddRows(numRows);
        }

        private List<int[]> rows = new List<int[]> { new int[] { 1 }, new int[] { 1, 1 } };

        private void AddRows(int count)
        {
            var lastRow = rows.Last();
            for (int i = 0; i < count; i++)
            {
                var newRow = new int[lastRow.Length + 1];
                for (int c = 0; c < newRow.Length; c++)
                {
                    if (c == 0 || c == newRow.Length - 1) newRow[c] = 1;
                    else newRow[c] = lastRow[c] + lastRow[c - 1];
                }
                rows.Add(newRow);
                lastRow = newRow;
            }
        }

        public int[] this[int row]
        {
            get
            {
                if (this.rows.Count <= row) AddRows(row - this.rows.Count + 1);
                return rows[row];
            }
        }

        public static PascalsTriangle Instance = new PascalsTriangle(20);
    }
}
