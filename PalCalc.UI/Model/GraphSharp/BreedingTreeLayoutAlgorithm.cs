using GraphSharp.Algorithms.Layout.Simple.Tree;
using GraphSharp.Algorithms.Layout;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PalCalc.Solver;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Animation;

namespace PalCalc.UI.Model
{
    internal partial class BreedingTreeLayoutAlgorithmParameters : ObservableObject, ILayoutParameters
    {
        [ObservableProperty]
        private double vertexGap = 10;

        [ObservableProperty]
        private double layerGap = 50;

        public object Clone()
        {
            return new BreedingTreeLayoutAlgorithmParameters()
            {
                VertexGap = VertexGap,
                LayerGap = LayerGap,
            };
        }
    }

    // currently assuming left-to-right layout
    internal class BreedingTreeLayoutAlgorithm : DefaultParameterizedLayoutAlgorithmBase<BreedingTreeNodeViewModel, BreedingEdge, BreedingGraph, BreedingTreeLayoutAlgorithmParameters>

    {
        private Dictionary<BreedingTreeNodeViewModel, Size> _sizes;

        public BreedingTreeLayoutAlgorithm(BreedingGraph visitedGraph, IDictionary<BreedingTreeNodeViewModel, Point> vertexPositions, IDictionary<BreedingTreeNodeViewModel, Size> vertexSizes, BreedingTreeLayoutAlgorithmParameters parameters)
            : base(visitedGraph, vertexPositions, parameters)
        {
            _sizes = new Dictionary<BreedingTreeNodeViewModel, Size>(vertexSizes);
        }

        private Size FullSizeWithChildren(BreedingTreeNodeViewModel node)
        {
            var childrenByDepth = node.Value.TraversedTopDown(0).GroupBy(p => p.Item2).ToDictionary(g => g.Key, g => g.Select(p => p.Item1).ToList());

            var maxWidthByDepth = childrenByDepth.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Max(n => _sizes[VisitedGraph.NodeFor(n)].Width));
            var minHeightByDepth = childrenByDepth.ToDictionary(
                kvp => kvp.Key,
                kvp => Parameters.VertexGap * (kvp.Value.Count - 1) + kvp.Value.Sum(n => _sizes[VisitedGraph.NodeFor(n)].Height)
            );

            var fullWidth = (childrenByDepth.Count - 1) * Parameters.LayerGap + maxWidthByDepth.Values.Sum();
            var fullHeight = minHeightByDepth.Values.Max();

            return new Size(fullWidth, fullHeight);
        }

        protected override void InternalCompute()
        {
            var orderedNodesByDepth = VisitedGraph.Tree.AllNodes
                .GroupBy(p => p.Item2)
                .ToDictionary(g => g.Key, g => g.Select(p => VisitedGraph.NodeFor(p.Item1)).ToList());

            var fullSize = FullSizeWithChildren(VisitedGraph.NodeFor(VisitedGraph.Tree.Root));
            var layerX = fullSize.Width / 2;

            // start from root node, center-right
            foreach (var kvp in orderedNodesByDepth.OrderBy(kvp => kvp.Key))
            {
                layerX -= kvp.Value.Max(n => _sizes[n].Width);
                layerX -= Parameters.LayerGap;

                var depth = kvp.Key;
                foreach (var node in kvp.Value)
                {
                    if (depth == 0)
                    {
                        VertexPositions[node] = new Point(layerX, 0);
                        continue;
                    }

                    var parentNode = orderedNodesByDepth[depth - 1].Single(p => p.Value.Children.Contains(node.Value));

                    var parentHeight = _sizes[parentNode].Height;
                    var parentPos = VertexPositions[parentNode];
                    parentPos.Y += parentHeight / 2;

                    var selfTotalHeight = FullSizeWithChildren(node).Height;
                    var parentTotalHeight = FullSizeWithChildren(parentNode).Height;
                    

                    bool isFirstChild = parentNode.Value.Children.First() == node.Value;

                    var selfY = isFirstChild
                        ? parentPos.Y - parentTotalHeight / 2
                        : parentPos.Y + Parameters.VertexGap;

                    VertexPositions[node] = new Point(layerX, selfY);
                }
            }

            // current positions are roughly accurate, but center each parent within their children
            foreach (var depth in orderedNodesByDepth.Keys.OrderByDescending(d => d))
            {
                foreach (var node in orderedNodesByDepth[depth])
                {
                    if (node.Value.Children.Any())
                    {
                        var parent1 = VisitedGraph.NodeFor(node.Value.Children.First());
                        var parent2 = VisitedGraph.NodeFor(node.Value.Children.Last());

                        var parent1Pos = VertexPositions[parent1];
                        var parent2Pos = VertexPositions[parent2];

                        var parent1Center = new Point(
                            parent1Pos.X + _sizes[parent1].Width / 2,
                            parent1Pos.Y + _sizes[parent1].Height / 2
                        );

                        var parent2Center = new Point(
                            parent2Pos.X + _sizes[parent2].Width / 2,
                            parent2Pos.Y + _sizes[parent2].Height / 2
                        );

                        var newPos = new Point(
                            VertexPositions[node].X,
                            (parent1Center.Y + parent2Center.Y) / 2 - _sizes[node].Height / 2
                        );

                        VertexPositions[node] = newPos;
                    }
                }
            }
        }
    }
}
