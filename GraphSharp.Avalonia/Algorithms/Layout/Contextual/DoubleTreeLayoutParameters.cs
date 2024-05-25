namespace GraphSharp.Algorithms.Layout.Contextual
{
	public enum DoubleTreeSides
	{
		Side1,
		Side2
	}

	public class DoubleTreeLayoutParameters : LayoutParametersBase
	{
		private LayoutDirection direction = LayoutDirection.LeftToRight;
		/// <summary>
		/// Gets or sets the layout direction.
		/// </summary>
		public LayoutDirection Direction
		{
			get { return direction; }
			set
			{
				if ( direction != value )
				{
					direction = value;
					NotifyPropertyChanged( "Direction" );
				}
			}
		}

		private double layerGap = 10;
		/// <summary>
		/// Gets or sets the gap between the layers.
		/// </summary>
		public double LayerGap
		{
			get { return layerGap; }
			set
			{
				if ( layerGap != value )
				{
					layerGap = value;
					NotifyPropertyChanged( "LayerGap" );
				}
			}
		}

		private double vertexGap = 10;
		/// <summary>
		/// Gets or sets the gap between the neighbor vertices in a layer.
		/// </summary>
		public double VertexGap
		{
			get { return vertexGap; }
			set
			{
				if ( vertexGap != value )
				{
					vertexGap = value;
					NotifyPropertyChanged( "VertexGap" );
				}
			}
		}

		private DoubleTreeSides prioritizedTreeSide = DoubleTreeSides.Side1;
		/// <summary>
		/// Gets or sets the the prioritized tree side (the one with the bigger priority: where a vertex 
		/// goes if it should go on both side).
		/// </summary>
		public DoubleTreeSides PrioritizedTreeSide
		{
			get { return prioritizedTreeSide; }
			set
			{
				if ( prioritizedTreeSide != value )
				{
					prioritizedTreeSide = value;
					NotifyPropertyChanged( "PrioritizedTreeSide" );
				}
			}
		}
	}
}