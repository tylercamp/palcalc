using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using GraphSharp.Helpers;
using System.Windows.Media;

namespace GraphSharp.Controls
{
	public partial class EdgeControl : Control, IPoolObject, IDisposable
	{
		#region Dependency Properties

	    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source",
	        typeof(VertexControl), typeof(EdgeControl), new PropertyMetadata(null, SourceChangedCallback));

	    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register("Target",
	        typeof(VertexControl), typeof(EdgeControl), new PropertyMetadata(null, TargetChangedCallback));

		public static readonly DependencyProperty RoutePointsProperty = DependencyProperty.Register( "RoutePoints",
				typeof( Point[] ),typeof( EdgeControl ),
				new UIPropertyMetadata( null ) );

		public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register( "Edge", typeof( object ),
																							 typeof( EdgeControl ),
																							 new PropertyMetadata( null ) );

        public static readonly DependencyProperty StrokeThicknessProperty = Shape.StrokeThicknessProperty.AddOwner( typeof(EdgeControl),
                                                                                                                    new UIPropertyMetadata(2.0) );

		public static readonly DependencyProperty FillProperty = Shape.FillProperty.AddOwner( typeof(EdgeControl),
																									new UIPropertyMetadata(null) );
		#endregion

		#region Properties
		public VertexControl Source
		{
			get { return (VertexControl)GetValue( SourceProperty ); }
			set { SetValue( SourceProperty, value ); }
		}

		public VertexControl Target
		{
			get { return (VertexControl)GetValue( TargetProperty ); }
			set { SetValue( TargetProperty, value ); }
		}

		public Point[] RoutePoints
		{
			get { return (Point[])GetValue( RoutePointsProperty ); }
			set { SetValue( RoutePointsProperty, value ); }
		}

		public object Edge
		{
			get { return GetValue( EdgeProperty ); }
			set { SetValue( EdgeProperty, value ); }
		}

        public double StrokeThickness
        {
            get { return (double)GetValue(StrokeThicknessProperty); }
            set { SetValue(StrokeThicknessProperty, value); }
        }

		public Brush Fill
		{
			get { return (Brush)GetValue(FillProperty); }
			set { SetValue(FillProperty, value); }
		}
		#endregion

		static EdgeControl()
		{
			//override the StyleKey
			DefaultStyleKeyProperty.OverrideMetadata( typeof( EdgeControl ), new FrameworkPropertyMetadata( typeof( EdgeControl ) ) );
		}

		#region IPoolObject Members

		public void Reset()
		{
			Edge = null;
			RoutePoints = null;
			Source = null;
			Target = null;
		}

		public void Terminate()
		{
			//nothing to do, there are no unmanaged resources
		}

		public event DisposingHandler Disposing;

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			if ( Disposing != null )
				Disposing( this );
		}

		#endregion
	}
}