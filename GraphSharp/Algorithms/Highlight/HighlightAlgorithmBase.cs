using System.ComponentModel;
using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
    public abstract class HighlightAlgorithmBase<TVertex, TEdge, TGraph, TParameters> : IHighlightAlgorithm<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
        where TParameters : class, IHighlightParameters
    {
        public IHighlightController<TVertex, TEdge, TGraph> Controller { get; private set; }

        IHighlightParameters IHighlightAlgorithm<TVertex, TEdge, TGraph>.Parameters
        {
            get { return Parameters; }
        }

        public TParameters Parameters { get; private set; }

        protected HighlightAlgorithmBase(
            IHighlightController<TVertex, TEdge, TGraph> controller,
            IHighlightParameters parameters )
        {
            Controller = controller;
            TrySetParameters( parameters );
        }

        public abstract void ResetHighlight();
        public abstract bool OnVertexHighlighting( TVertex vertex );
        public abstract bool OnVertexHighlightRemoving( TVertex vertex );
        public abstract bool OnEdgeHighlighting( TEdge edge );
        public abstract bool OnEdgeHighlightRemoving( TEdge edge );

        public bool IsParametersSettable( IHighlightParameters parameters )
        {
            return parameters != null && parameters is TParameters;
        }

        public bool TrySetParameters( IHighlightParameters parameters )
        {
            if ( IsParametersSettable( parameters ) )
            {
                if ( Parameters != null )
                    Parameters.PropertyChanged -= OnParameterPropertyChanged;
                Parameters = (TParameters)parameters;
                if ( Parameters != null )
                    Parameters.PropertyChanged += OnParameterPropertyChanged;
                return true;
            }
            return false;
        }

        protected virtual void OnParametersChanged()
        {
            this.ResetHighlight();
        }

        private void OnParameterPropertyChanged( object sender, PropertyChangedEventArgs e )
        {
            OnParametersChanged();
        }
    }
}