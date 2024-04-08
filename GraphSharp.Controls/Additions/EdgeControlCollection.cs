using System.Collections.ObjectModel;

namespace GraphSharp.Controls
{
    public class EdgeControlCollection : Collection<EdgeControl>
    {
        public void AddEdge(EdgeControl edgeControl)
        {
            if (edgeControl == null)
            {
                return;
            }
            Add(edgeControl);
        }

        public void RemoveEdge(EdgeControl edgeControl)
        {
            if (edgeControl == null)
            {
                return;
            }
            Remove(edgeControl);
        }

    }
}