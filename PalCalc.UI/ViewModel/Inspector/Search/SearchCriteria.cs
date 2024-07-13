using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface ISearchCriteria
    {
        bool Matches(PalInstance pal);
    }

    public class TraitSearchCriteria(Trait trait) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => pal.Traits.Contains(trait);
    }

    public class PalSearchCriteria(Pal palType) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => pal.Pal == palType;
    }

    public class GenderSearchCriteria(PalGender gender) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => pal.Gender == gender;
    }

    public class CustomSearchCriteria(Func<PalInstance, bool> f) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => f(pal);
    }

    public class AllOfSearchCriteria(IEnumerable<ISearchCriteria> criteria) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => !criteria.Any(c => !c.Matches(pal));
    }
    
    public class AnyOfSearchCriteria(IEnumerable<ISearchCriteria> criteria) : ISearchCriteria
    {
        public bool Matches(PalInstance pal) => criteria.Any(c => c.Matches(pal));
    }
}
