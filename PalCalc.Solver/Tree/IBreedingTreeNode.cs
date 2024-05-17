using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public interface IBreedingTreeNode
    {
        IPalReference PalRef { get; }
        IEnumerable<IBreedingTreeNode> Children { get; }

        IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth);

        IEnumerable<string> DescriptionLines { get; }
        string Description { get; }
    }
}
