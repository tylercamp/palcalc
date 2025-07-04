using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class SurgeryPalNode(SurgeryTablePalReference pref, IBreedingTreeNode inputNode) : IBreedingTreeNode
    {
        public IPalReference PalRef => pref;

        public IEnumerable<IBreedingTreeNode> Children => [inputNode];

        public IEnumerable<string> DescriptionLines => [
            $"Surgery on {PalRef.Pal.Name}",
            .. ((SurgeryTablePalReference)PalRef).Operations.Select(op => op.ToString()),
            $"Costs {pref.Operations.Sum(op => op.GoldCost)}"
        ];

        public string Description => string.Join('\n', DescriptionLines);

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);

            foreach (var n in inputNode.TraversedTopDown(currentDepth + 1))
                yield return n;
        }
    }
}
