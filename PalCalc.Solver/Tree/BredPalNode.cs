using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class BredPalNode : IBreedingTreeNode
    {
        public BredPalNode(BredPalReference bpr, IBreedingTreeNode parentNode1, IBreedingTreeNode parentNode2)
        {
            PalRef = bpr;
            Children = new List<IBreedingTreeNode>() { parentNode1, parentNode2 };

            ParentNode1 = parentNode1;
            ParentNode2 = parentNode2;
        }

        public string Description => string.Join('\n', DescriptionLines);

        public IBreedingTreeNode ParentNode1 { get; }
        public IBreedingTreeNode ParentNode2 { get; }

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children { get; }

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            foreach (var n in ParentNode1.TraversedTopDown(currentDepth + 1))
                yield return n;

            yield return (this, currentDepth);

            foreach (var n in ParentNode2.TraversedTopDown(currentDepth + 1))
                yield return n;
        }

        public IEnumerable<string> DescriptionLines
        {
            get
            {
                var asBred = PalRef as BredPalReference;
                yield return $"Bred {asBred.Pal.Name}";
                yield return $"{asBred.Gender} gender w/ {asBred.EffectivePassives.ModelObjects.PassiveSkillListToString()}";
                yield return $"takes ~{asBred.SelfBreedingEffort} for {asBred.AvgRequiredBreedings} breed attempts";
            }
        }
    }
}
