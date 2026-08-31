using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class DirectPalNode : IBreedingTreeNode
    {
        public DirectPalNode(IPalReference pref)
        {
            PalRef = pref;
        }

        public string Description => string.Join('\n', DescriptionLines);

        public IPalReference PalRef { get; }
        public IEnumerable<IBreedingTreeNode> Children { get; } = Enumerable.Empty<IBreedingTreeNode>();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }

        public IEnumerable<string> DescriptionLines
        {
            get
            {
                switch (PalRef)
                {
                    case WildPalReference wild:
                        yield return $"Wild {wild.Pal.Name}";
                        yield return $"{wild.Gender} gender w/ up to {wild.EffectivePassives.Count} random passives";
                        break;

                    case OwnedPalReference owned:
                        yield return $"Owned {owned.Pal.Name}";
                        yield return $"in {owned.Location}";
                        yield return $"{owned.Gender} w/ {owned.EffectivePassives.PassiveSkillListToString()}";
                        break;

                    default: throw new NotImplementedException();
                }
            }
        }
    }

}
