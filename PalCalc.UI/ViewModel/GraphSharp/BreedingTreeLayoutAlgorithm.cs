using GraphSharp.Algorithms.Layout.Simple.Tree;
using GraphSharp.Algorithms.Layout;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Animation;
using QuickGraph.Serialization;
using PalCalc.Solver.Tree;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    internal partial class BreedingTreeLayoutAlgorithmParameters : ObservableObject, ILayoutParameters
    {
        [ObservableProperty]
        private double vertexGap = 10;

        [ObservableProperty]
        private double layerGap = 10;

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
    internal class BreedingTreeLayoutAlgorithm : DefaultParameterizedLayoutAlgorithmBase<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph, BreedingTreeLayoutAlgorithmParameters>
    {
        private Dictionary<IBreedingTreeNodeViewModel, Size> _sizes;
        private Dictionary<IBreedingTreeNode, IBreedingTreeNodeViewModel> _viewmodels;
        private Dictionary<IBreedingTreeNodeViewModel, Size> _fullSizes = new Dictionary<IBreedingTreeNodeViewModel, Size>();
        private Dictionary<IBreedingTreeNodeViewModel, Node> _nodesByVm;
        private Dictionary<IBreedingTreeNode, Node> _nodesByModel;

        private class Node
        {
            private BreedingTreeLayoutAlgorithm algo;

            public Node(BreedingTreeLayoutAlgorithm algo, IBreedingTreeNodeViewModel asViewModel, IBreedingTreeNode asModel)
            {
                this.algo = algo;
                AsViewModel = asViewModel;
                AsModel = asModel;
            }

            public IBreedingTreeNodeViewModel AsViewModel { get; }
            public IBreedingTreeNode AsModel { get; }

            public Point SelfCenter
            {
                get => algo.VertexPositions[AsViewModel];
                set => algo.VertexPositions[AsViewModel] = value;
            }
            public Size SelfSize => algo._sizes[AsViewModel];

            public Size FullSize => algo.FullSizeWithChildren(AsViewModel);

            public double SelfTopY
            {
                get => SelfCenter.Y - SelfSize.Height / 2;
                set
                {
                    var p = SelfCenter;
                    p.Y = value + SelfSize.Height / 2;
                    SelfCenter = p;
                }
            }

            public double SelfBottomY
            {
                get => SelfCenter.Y + SelfSize.Height / 2;
                set
                {
                    var p = SelfCenter;
                    p.Y = value - SelfSize.Height / 2;
                    SelfCenter = p;
                }
            }

            public double FullTopY => SelfCenter.Y - FullSize.Height / 2;
            public double FullBottomY => SelfCenter.Y + FullSize.Height / 2;
        }

        public BreedingTreeLayoutAlgorithm(BreedingGraph visitedGraph, IDictionary<IBreedingTreeNodeViewModel, Point> vertexPositions, IDictionary<IBreedingTreeNodeViewModel, Size> vertexSizes, BreedingTreeLayoutAlgorithmParameters parameters)
            : base(visitedGraph, vertexPositions, parameters)
        {
            _sizes = new Dictionary<IBreedingTreeNodeViewModel, Size>(vertexSizes);
            _viewmodels = visitedGraph.Vertices.ToDictionary(vm => vm.Value);

            _nodesByModel = new Dictionary<IBreedingTreeNode, Node>();
            _nodesByVm = new Dictionary<IBreedingTreeNodeViewModel, Node>();
            foreach (var vm in _viewmodels.Values)
            {
                var node = new Node(this, vm, vm.Value);
                _nodesByModel.Add(vm.Value, node);
                _nodesByVm.Add(vm, node);
            }
        }

        private Size FullSizeWithChildren(IBreedingTreeNodeViewModel node)
        {
            if (_fullSizes.ContainsKey(node)) return _fullSizes[node];

            if (!node.Value.Children.Any())
            {
                var size = _sizes[node];
                _fullSizes[node] = size;
                return size;
            }

            Size fullSize = new Size(
                width: node.Value.Children.Max(n => FullSizeWithChildren(_viewmodels[n]).Width) + Parameters.LayerGap + _sizes[node].Width,
                height: Math.Max(node.Value.Children.Sum(c => FullSizeWithChildren(_viewmodels[c]).Height) + Parameters.VertexGap, _sizes[node].Height)
            );

            _fullSizes.Add(node, fullSize);
            return fullSize;
        }

        protected override void InternalCompute()
        {
            var orderedNodesByDepth = VisitedGraph.Tree.AllNodes
                .GroupBy(p => p.Item2)
                .ToDictionary(g => g.Key, g => g.Select(p => VisitedGraph.NodeFor(p.Item1)).ToList());

            var fullSize = FullSizeWithChildren(VisitedGraph.NodeFor(VisitedGraph.Tree.Root));
            var layerCenterX = fullSize.Width / 2;

            // node positions are centered within the node
            // start from root node, center-right
            double prevWidth = 0;
            foreach (var kvp in orderedNodesByDepth.OrderBy(kvp => kvp.Key))
            {
                var depth = kvp.Key;
                var width = kvp.Value.Max(n => _sizes[n].Width);

                if (depth == 0)
                {
                    layerCenterX -= width / 2; // properly align right-most (root) node with right side of tree
                }
                else
                {
                    layerCenterX -= prevWidth / 2;
                    layerCenterX -= Parameters.LayerGap;
                    layerCenterX -= width / 2;
                }

                foreach (var vmNode in kvp.Value)
                {
                    var node = _nodesByVm[vmNode];

                    if (depth == 0)
                    {
                        VertexPositions[vmNode] = new Point(layerCenterX, 0);
                        continue;
                    }

                    // all of these nodes (depth > 0) will have a parent and a sibling

                    // self Y will be the center of child Ys.
                    // child Ys are based on their full height / 2

                    // ('Y' in this comment block refers to top of node)
                    // parent Y will be avg(child1y [top], child2y [bottom])
                    //    child1y = parentFullTopY + child1FullHeight / 2
                    //    child2y = parentFullBottomY - child2FullHeight / 2

                    var parentVm = orderedNodesByDepth[depth - 1].Single(p => p.Value.Children.Contains(vmNode.Value));
                    var parentNode = _nodesByVm[parentVm];

                    bool isFirstChild = parentVm.Value.Children.First() == vmNode.Value;
                    double nodeCenterY;
                    if (isFirstChild)
                    {
                        nodeCenterY = parentNode.FullTopY + node.FullSize.Height / 2;
                    }
                    else
                    {
                        nodeCenterY = parentNode.FullBottomY - node.FullSize.Height / 2;
                    }

                    node.SelfCenter = new Point(layerCenterX, nodeCenterY);
                }

                prevWidth = width;
            }

            // current positions are roughly accurate, but center each parent within their children
            foreach (var depth in orderedNodesByDepth.Keys.OrderByDescending(d => d))
            {
                foreach (var nodeVm in orderedNodesByDepth[depth])
                {
                    if (nodeVm.Value.Children.Any())
                    {
                        var node = _nodesByVm[nodeVm];

                        var parent1 = VisitedGraph.NodeFor(nodeVm.Value.Children.First());
                        var parent2 = VisitedGraph.NodeFor(nodeVm.Value.Children.Last());

                        var p1Node = _nodesByVm[parent1];
                        var p2Node = _nodesByVm[parent2];

                        node.SelfCenter = new Point(node.SelfCenter.X, (p1Node.SelfBottomY + p2Node.SelfTopY) / 2);
                    }
                }
            }
        }
    }
}
