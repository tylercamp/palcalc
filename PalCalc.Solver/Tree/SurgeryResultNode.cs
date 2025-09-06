using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class SurgeryResultNode : IBreedingTreeNode
    {
        public SurgeryResultNode(SurgeryTablePalReference pref, IBreedingTreeNode inputNode)
        {
            PalRef = pref;
            this.inputNode = inputNode;
            this.operationNode = new SurgeryOperationNode(pref);
        }

        private IBreedingTreeNode inputNode, operationNode;

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children => [inputNode, operationNode];

        public IEnumerable<string> DescriptionLines
        {
            get
            {
                var asSurgery = PalRef as SurgeryTablePalReference;
                yield return $"Result of {asSurgery.Pal.Name} Surgery";
                yield return $"{asSurgery.Gender} gender w/ {asSurgery.EffectivePassives.PassiveSkillListToString()}";
            }
        }

        public string Description => throw new NotImplementedException();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            foreach (var n in operationNode.TraversedTopDown(currentDepth + 1))
                yield return n;

            yield return (this, currentDepth);

            foreach (var n in inputNode.TraversedTopDown(currentDepth + 1))
                yield return n;
        }
    }
}
