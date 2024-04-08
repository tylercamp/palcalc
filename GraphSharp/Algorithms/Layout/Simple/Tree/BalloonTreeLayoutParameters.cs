namespace GraphSharp.Algorithms.Layout.Simple.Tree
{
	public class BalloonTreeLayoutParameters : LayoutParametersBase
	{
		private int _minRadius = 2;
        private float _border = 20.0f;

		public int MinRadius
		{
			get { return _minRadius; }
			set
			{
				if ( value != _minRadius )
				{
					_minRadius = value;
					NotifyPropertyChanged( "MinRadius" );
				}
			}
		}

		public float Border
		{
			get { return _border; }
			set
			{
				if ( value != _border )
				{
					_border = value;
					NotifyPropertyChanged( "Border" );
				}
			}
		}
	}
}
