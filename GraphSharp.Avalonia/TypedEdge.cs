using QuickGraph;

namespace GraphSharp
{
	public enum EdgeTypes
	{
		General,
		Hierarchical
	}

	public class TypedEdge<TVertex> : Edge<TVertex>
	{
	    public EdgeTypes Type { get; private set; }

	    public TypedEdge(TVertex source, TVertex target, EdgeTypes type)
			: base(source, target)
		{
			Type = type;
		}

		public override string ToString()
		{
			return string.Format("{0}: {1}-->{2}", Type, Source, Target);
		}
	}
}