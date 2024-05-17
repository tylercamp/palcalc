using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class CompositePalNode : IBreedingTreeNode
    {
        public CompositePalNode(CompositeOwnedPalReference compositeRef)
        {
            Male = compositeRef.Male;
            Female = compositeRef.Female;

            PalRef = compositeRef;
        }

        public OwnedPalReference Male { get; }
        public OwnedPalReference Female { get; }

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children { get; } = Enumerable.Empty<IBreedingTreeNode>();

        public IEnumerable<string> DescriptionLines => throw new NotImplementedException();

        public string Description => throw new NotImplementedException();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }
    }
}
