using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public abstract class IResultPruning
    {
        protected CancellationToken token;
        public IResultPruning(CancellationToken token)
        {
            this.token = token;
        }

        public abstract IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results);

        protected static readonly IEnumerable<IPalReference> Empty = Enumerable.Empty<IPalReference>();
        protected IEnumerable<IPalReference> FirstGroupOf<T>(IEnumerable<IPalReference> input, Func<IPalReference, T> grouping)
        {
            try
            {
                if (token.IsCancellationRequested)
                    return Empty;

                var resultGroup = input
                    .TakeWhile(_ => !token.IsCancellationRequested)
                    .GroupBy(grouping)
                    .OrderBy(g => g.Key)
                    .FirstOrDefault();

                if (token.IsCancellationRequested)
                    return Empty;

                return resultGroup.ToList();
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested)
                    return input;
                else
                    throw;
            }
        }
    }
}
