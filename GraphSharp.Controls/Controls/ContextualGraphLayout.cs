using System.Collections.Generic;
using GraphSharp.Algorithms.Layout;
using QuickGraph;
using System.Windows;
using GraphSharp.Algorithms.Layout.Contextual;

namespace GraphSharp.Controls
{
	public class ContextualGraphLayout<TVertex, TEdge, TGraph> : GraphLayout<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		#region Dependency Properties

		/// <summary>Gets or sets the SelectedVertex which influences the Context.</summary>
		public TVertex SelectedVertex
		{
			get { return (TVertex)GetValue( SelectedVertexProperty ); }
			set { SetValue( SelectedVertexProperty, value ); }
		}

		public static readonly DependencyProperty SelectedVertexProperty = DependencyProperty.Register( "SelectedVertex", typeof( TVertex ), typeof( ContextualGraphLayout<TVertex, TEdge, TGraph> ), new UIPropertyMetadata( default( TVertex ), SelectedVertexPropertyChanged ) );

		private static void SelectedVertexPropertyChanged( DependencyObject obj, DependencyPropertyChangedEventArgs args )
		{
			var gl = obj as ContextualGraphLayout<TVertex, TEdge, TGraph>;
			if ( gl == null )
				return;

			//refresh the layout on context change
			gl.Relayout();
		}
		#endregion

		static ContextualGraphLayout() 
		{
			LayoutAlgorithmFactoryProperty.OverrideMetadata( typeof( ContextualGraphLayout<TVertex, TEdge, TGraph> ), new PropertyMetadata( new ContextualLayoutAlgorithmFactory<TVertex, TEdge, TGraph>(), null, LayoutAlgorithmFactoryCoerce ) );
		}

		protected override ILayoutContext<TVertex, TEdge, TGraph> CreateLayoutContext( IDictionary<TVertex, Point> positions, IDictionary<TVertex, Size> sizes )
		{
			return new ContextualLayoutContext<TVertex, TEdge, TGraph>( Graph, SelectedVertex, positions, sizes );
		}

	    protected override bool CanLayout
	    {
	        get { return SelectedVertex != null && base.CanLayout; }
	    }
	}
}