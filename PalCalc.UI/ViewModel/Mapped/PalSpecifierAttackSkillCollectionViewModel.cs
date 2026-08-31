using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Mapped;

public partial class PalSpecifierAttackSkillCollectionViewModel : ObservableObject
{
    public PalSpecifierAttackSkillCollectionViewModel()
    {
    }

    public PalSpecifierAttackSkillCollectionViewModel(IEnumerable<ActiveSkill> modelAttacks)
    {
        var attacks = modelAttacks.Take(PalSpecifier.MaxRequiredAttacks).Select(ActiveSkillViewModel.Make).ToArray();
        Attack1 = attacks.ElementAtOrDefault(0);
        Attack2 = attacks.ElementAtOrDefault(1);
        Attack3 = attacks.ElementAtOrDefault(2);
        Attack4 = attacks.ElementAtOrDefault(3);
        Attack5 = attacks.ElementAtOrDefault(4);
        Attack6 = attacks.ElementAtOrDefault(5);
    }

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack1;

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack2;

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack3;

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack4;

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack5;

    [NotifyPropertyChangedFor(nameof(HasItems))]
    [ObservableProperty]
    private ActiveSkillViewModel attack6;

    public bool HasItems => AsEnumerable().Any();

    public IEnumerable<ActiveSkillViewModel> AsEnumerable() =>
        new[] { Attack1, Attack2, Attack3, Attack4, Attack5, Attack6 }.OfType<ActiveSkillViewModel>();

    public IEnumerable<ActiveSkill> AsModelEnumerable() => AsEnumerable().Select(attack => attack.ModelObject).Distinct();

    public void CopyFrom(PalSpecifierAttackSkillCollectionViewModel other)
    {
        Attack1 = other.Attack1;
        Attack2 = other.Attack2;
        Attack3 = other.Attack3;
        Attack4 = other.Attack4;
        Attack5 = other.Attack5;
        Attack6 = other.Attack6;
    }
}
