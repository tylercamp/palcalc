using PalCalc.Solver.PalReference;
using PalCalc.Solver.Tree;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public interface ISurgeryOperationViewModel
    {
        ISurgeryOperation ModelObject { get; }

        static ISurgeryOperationViewModel FromModelObject(ISurgeryOperation modelObject) =>
            modelObject switch
            {
                AddPassiveSurgeryOperation apso => new AddPassiveSurgeryOperationViewModel(apso),
                ReplacePassiveSurgeryOperation rpso => new ReplacePassiveSurgeryOperationViewModel(rpso),
                _ => throw new NotImplementedException($"VM creation not handled for surgery type: {modelObject?.GetType()?.Name}")
            };
    }

    public class AddPassiveSurgeryOperationViewModel(AddPassiveSurgeryOperation op) : ISurgeryOperationViewModel
    {
        public ISurgeryOperation ModelObject => op;

        public PassiveSkillViewModel AddedPassive { get; } = PassiveSkillViewModel.Make(op.AddedPassive);
    }

    public class ReplacePassiveSurgeryOperationViewModel(ReplacePassiveSurgeryOperation op) : ISurgeryOperationViewModel
    {
        public ISurgeryOperation ModelObject => op;

        public PassiveSkillViewModel AddedPassive { get; } = PassiveSkillViewModel.Make(op.AddedPassive);
        public PassiveSkillViewModel RemovedPassive { get; } = PassiveSkillViewModel.Make(op.RemovedPassive);
    }

    // (note: this node just represents the breeding table itself, the result pal is a separate (parent) node)
    public partial class SurgeryBreedingTreeNodeViewModel : IBreedingTreeNodeViewModel
    {
        public SurgeryBreedingTreeNodeViewModel(SurgeryOperationNode node)
        {
            pref = node.PalRef as SurgeryTablePalReference;

            Value = node;
            Operations = pref.Operations.Select(ISurgeryOperationViewModel.FromModelObject).ToList();
            GoldCost = pref.Operations.Sum(op => op.GoldCost);
        }

        private SurgeryTablePalReference pref;

        public IBreedingTreeNode Value { get; }

        public List<ISurgeryOperationViewModel> Operations { get; }

        public int GoldCost { get; }
    }
}
