using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Tree;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public partial class StandardBreedingTreeNodeViewModel : ObservableObject, IBreedingTreeNodeViewModel, IRefreshableNode
    {
        public StandardBreedingTreeNodeViewModel(CachedSaveGame source, GameSettings settings, IBreedingTreeNode node)
        {
            Value = node;
            Pal = PalViewModel.Make(node.PalRef.Pal);
            PassiveSkills = node.PalRef.ActualPassives.Select(PassiveSkillViewModel.Make).ToList();
            PassiveSkillsCollection = new PassiveSkillCollectionViewModel(PassiveSkills);

            switch (node.PalRef.Location)
            {
                case CompositeRefLocation crl:
                    Location = new CompositePalRefLocationViewModel(source, settings, crl);
                    break;

                case CapturedRefLocation carl:
                    Location = new WildPalRefLocationViewModel();
                    break;

                default:
                    Location = new SpecificPalRefLocationViewModel(source, settings, node.PalRef.Location);
                    break;
            }

            Gender = PalGenderViewModel.Make(node.PalRef.Gender);
            AvgRequiredAttemptsDescription = LocalizationCodes.LC_RESULT_BREEDING_ATTEMPTS.Bind(AvgRequiredAttempts);

            IVs = IVSetViewModel.FromIVs(node.PalRef.IVs);
        }

        public PalViewModel Pal { get; }

        public IBreedingTreeNode Value { get; }

        public List<PassiveSkillViewModel> PassiveSkills { get; }

        public PassiveSkillCollectionViewModel PassiveSkillsCollection { get; }

        public Visibility PassiveSkillsVisibility => PassiveSkills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EffortVisibility => Value.PalRef.BreedingEffort > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;
        public string Effort
        {
            get
            {
                var effort = Value.PalRef.BreedingEffort;
                var selfEffort = Value.PalRef.SelfBreedingEffort;

                if (effort != selfEffort && Value.PalRef is BredPalReference) return $"{selfEffort.TimeSpanMinutesStr()} ({effort.TimeSpanMinutesStr()})";
                else return effort.TimeSpanMinutesStr();
            }
        }

        public IPalRefLocationViewModel Location { get; }
        public bool NeedsRefresh => Location.NeedsRefresh;

        public PalGenderViewModel Gender { get; }

        public int AvgRequiredAttempts => (Value.PalRef as BredPalReference)?.AvgRequiredBreedings ?? 0;
        public ILocalizedText AvgRequiredAttemptsDescription { get; }

        public Visibility AvgRequiredAttemptsVisibility => Value.PalRef is BredPalReference ? Visibility.Visible : Visibility.Collapsed;

        public IVSetViewModel IVs { get; }
    }
}
