using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class SurgeryOperationNode(SurgeryTablePalReference pref) : IBreedingTreeNode
    {
        public IPalReference PalRef => pref;

        public IEnumerable<IBreedingTreeNode> Children => [];

        public IEnumerable<string> DescriptionLines => [
            $"Surgery on {pref.Pal.Name}",
            .. pref.Operations.Select(op => op.ToString()),
            $"Costs {pref.Operations.Sum(op => op.GoldCost)}"
        ];

        public string Description => string.Join('\n', DescriptionLines);

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }
    }
}
