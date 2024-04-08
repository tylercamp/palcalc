using QuickGraph;

namespace GraphSharp
{
	public class WeightedEdge<TVertex> : Edge<TVertex>
	{
		public double Weight { get; private set; }

	    public WeightedEdge(TVertex source, TVertex target, double weight = 1)
			: base(source, target)
		{
			Weight = weight;
		}
	}
}