using System.Windows;

namespace GraphSharp.Controls
{
    public partial class EdgeControl
    {


        private static void SourceChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            EdgeControl c = d as EdgeControl;

            VertexControl vcn = e.NewValue as VertexControl;
            if (vcn != null)
            {
                vcn.AsSources.AddEdge(c);
            }
            VertexControl vco = e.OldValue as VertexControl;
            if (vco != null)
            {
                vco.AsSources.RemoveEdge(c);
            }
        }

        private static void TargetChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            EdgeControl c = d as EdgeControl;

            VertexControl vcn = e.NewValue as VertexControl;
            if (vcn != null)
            {
                vcn.AsTargets.AddEdge(c);
            }
            VertexControl vco = e.OldValue as VertexControl;
            if (vco != null)
            {
                vco.AsTargets.RemoveEdge(c);
            }
        }

    }
}