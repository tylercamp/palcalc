using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public partial class BreedingTreeNodeViewModel : ObservableObject
    {
        public BreedingTreeNodeViewModel(CachedSaveGame source, IBreedingTreeNode node)
        {
            Value = node;
            Pal = new PalViewModel(node.PalRef.Pal);
            Traits = node.PalRef.ActualTraits.Select(t => new TraitViewModel(t)).ToList();
            TraitCollection = new TraitCollectionViewModel(Traits);

            switch (node.PalRef.Location)
            {
                case CompositeRefLocation crl:
                    Location = new CompositePalRefLocationViewModel(source, crl);
                    break;

                case CapturedRefLocation carl:
                    Location = new WildPalRefLocationViewModel();
                    break;

                default:
                    Location = new SpecificPalRefLocationViewModel(source, node.PalRef.Location);
                    break;
            }

            Gender = node.PalRef.Gender switch
            {
                PalGender.MALE => "Male",
                PalGender.FEMALE => "Female",
                PalGender.WILDCARD => "Any Gender",
                PalGender.OPPOSITE_WILDCARD => "Opposite Gender",
                _ => "Unknown Gender"
            };
        }

        public PalViewModel Pal { get; }

        public IBreedingTreeNode Value { get; }

        public List<TraitViewModel> Traits { get; }

        public TraitCollectionViewModel TraitCollection { get; }

        public Visibility TraitsVisibility => Traits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

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

        public string Gender { get; }

        public int AvgRequiredAttempts => (Value.PalRef as BredPalReference)?.AvgRequiredBreedings ?? 0;
        public string AvgRequiredAttemptsDescription => $"{AvgRequiredAttempts} attempts";

        public Visibility AvgRequiredAttemptsVisibility => Value.PalRef is BredPalReference ? Visibility.Visible : Visibility.Collapsed;
    }
}
