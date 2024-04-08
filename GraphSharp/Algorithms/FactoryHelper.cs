using GraphSharp.Algorithms.Layout;

namespace GraphSharp.Algorithms
{
    public static class FactoryHelper
    {
        public static TParam CreateNewParameter<TParam>(this ILayoutParameters oldParameters) where TParam : class, ILayoutParameters, new()
        {
            return oldParameters is TParam
                    ? (TParam) (oldParameters as TParam).Clone()
                    : new TParam();
        }
    }
}