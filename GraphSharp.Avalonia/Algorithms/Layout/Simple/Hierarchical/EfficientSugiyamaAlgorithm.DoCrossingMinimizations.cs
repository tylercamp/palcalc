using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QuickGraph;

namespace GraphSharp.Algorithms.Layout.Simple.Hierarchical
{
    public partial class EfficientSugiyamaLayoutAlgorithm<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
    {
        private readonly Random _rnd = new Random(DateTime.Now.Millisecond);

        private int[] _crossCounts;

        private IList<Edge<Data>>[] _sparseCompactionByLayerBackup;

        private AlternatingLayer[] _alternatingLayers;

        /// <summary>Minimizes the crossings between the layers by sweeping up and down while there could be something optimized.</summary>
        private void DoCrossingMinimizations()
        {
            int prevCrossings;
            int crossings = int.MaxValue;
            int phase = 1;

            _crossCounts = new int[_layers.Count];
            _sparseCompactionByLayerBackup = new IList<Edge<Data>>[_layers.Count];
            _alternatingLayers = new AlternatingLayer[_layers.Count];
            for (int i = 0; i < _layers.Count; i++)
                _crossCounts[i] = int.MaxValue;

            int phase1IterationLeft = 100;
            int phase2IterationLeft = _layers.Count;
            bool changed;
            bool wasPhase2;
            do
            {
                changed = false;
                prevCrossings = crossings;
                if (phase == 1)
                    phase1IterationLeft--;
                else if (phase == 2)
                    phase2IterationLeft--;
                wasPhase2 = (phase == 2);

                bool c;
                crossings = Sweeping(0, _layers.Count - 1, 1, true, out c, ref phase);
                changed |= c;
                if (crossings == 0)
                    break;

                crossings = Sweeping(_layers.Count - 1, 0, -1, true, out c, ref phase);
                changed = changed || c;
                if (phase == 1 && (!changed || crossings >= prevCrossings) && phase2IterationLeft > 0)
                    phase = 2;
                else if (phase == 2)
                    phase = 1;
            } while (crossings > 0 
                && ((phase2IterationLeft > 0 || wasPhase2) 
                    || (phase1IterationLeft > 0 && (crossings < prevCrossings) && changed)));
        }

        /// <summary>
        /// Sweeps between the <paramref name="startLayerIndex"/> and <paramref name="endLayerIndex"/>
        /// in the way represented by the step 
        /// </summary>
        /// <param name="startLayerIndex">The index of the start layer (where the sweeping starts from).</param>
        /// <param name="endLayerIndex">The index of the last layer (where the sweeping ends).</param>
        /// <param name="step">Increment or decrement of the layer index. (1 or -1)</param>
        /// <returns>The number of the edge crossings.</returns>
        private int Sweeping(int startLayerIndex, int endLayerIndex, int step, bool enableSameMeasureOptimization, out bool changed, ref int phase)
        {
            int crossings = 0;
            changed = false;
            if (_alternatingLayers.Length > 0)
            {
                AlternatingLayer alternatingLayer;
                if (_alternatingLayers[startLayerIndex] == null)
                {
                    alternatingLayer = new AlternatingLayer();
                    alternatingLayer.AddRange(_layers[startLayerIndex].OfType<IData>());
                    alternatingLayer.EnsureAlternatingAndPositions();
                    AddAlternatingLayerToSparseCompactionGraph(alternatingLayer, startLayerIndex);
                    _alternatingLayers[startLayerIndex] = alternatingLayer;
                }
                else
                    alternatingLayer = _alternatingLayers[startLayerIndex];

                OutputAlternatingLayer(alternatingLayer, startLayerIndex, 0);
                for (int i = startLayerIndex; i != endLayerIndex; i += step)
                {
                    int ci = Math.Min(i, i + step);
                    int prevCrossCount = _crossCounts[ci];

                    if (_alternatingLayers[i + step] != null)
                    {
                        alternatingLayer.SetPositions();
                        _alternatingLayers[i + step].SetPositions();
                        prevCrossCount = DoCrossCountingAndOptimization(alternatingLayer, _alternatingLayers[i + step], (i < i + step), false, (phase == 2), int.MaxValue);
                        _crossCounts[ci] = prevCrossCount;
                    }

                    int crossCount = CrossingMinimizationBetweenLayers(ref alternatingLayer, i, i + step, enableSameMeasureOptimization, prevCrossCount, phase);

                    if (crossCount < prevCrossCount || phase == 2 || changed)
                    {
                        /* set the sparse compaction graph */
                        AddAlternatingLayerToSparseCompactionGraph(alternatingLayer, i + step);
                        ReplaceLayer(alternatingLayer, i + step);
                        _alternatingLayers[i + step] = alternatingLayer;
                        OutputAlternatingLayer(alternatingLayer, i + step, crossCount);
                        _crossCounts[i] = crossCount;
                        crossings += crossCount;
                        changed = true;
                    }
                    else
                    {
                        Debug.WriteLine("Layer " + (i + step) + " has not changed.");
                        alternatingLayer = _alternatingLayers[i + step];
                        crossings += prevCrossCount;
                    }
                }
            }
            return crossings;
        }

        [Conditional("DEBUG")]
        private static void OutputAlternatingLayer(AlternatingLayer alternatingLayer, int layerIndex, int crossCount)
        {
            Debug.Write(layerIndex + " | " + crossCount + ": ");
            foreach (var element in alternatingLayer)
            {
                if (element is SugiVertex)
                {
                    var vertex = element as SugiVertex;
                    Debug.Write(string.Format("{0},{1}\t", vertex.OriginalVertex, vertex.Type.ToString()[0]));
                }
                else
                {
                    var segmentContainer = element as SegmentContainer;
                    if (segmentContainer == null)
                        continue;

                    for (int j = 0; j < segmentContainer.Count; j++)
                        Debug.Write("| \t");
                }
            }
            Debug.WriteLine("");
        }

        private void ReplaceLayer(AlternatingLayer alternatingLayer, int i)
        {
            _layers[i].Clear();
            foreach (var item in alternatingLayer)
            {
                var vertex = item as SugiVertex;
                if (vertex == null)
                    continue;
                _layers[i].Add(vertex);
                vertex.IndexInsideLayer = i;
            }
        }

        private int CrossingMinimizationBetweenLayers(
            ref AlternatingLayer alternatingLayer,
            int actualLayerIndex,
            int nextLayerIndex,
            bool enableSameMeasureOptimization,
            int prevCrossCount,
            int phase)
        {
            //decide which way we are sweeping (up or down)
            //straight = down, reverse = up
            bool straightSweep = (actualLayerIndex < nextLayerIndex);
            var nextAlternatingLayer = alternatingLayer.Clone();

            /* 1 */
            AppendSegmentsToAlternatingLayer(nextAlternatingLayer, straightSweep);

            /* 2 */
            ComputeMeasureValues(alternatingLayer, nextLayerIndex, straightSweep);
            nextAlternatingLayer.SetPositions();

            /* 3 */
            nextAlternatingLayer = InitialOrderingOfNextLayer(nextAlternatingLayer, _layers[nextLayerIndex], straightSweep);

            /* 4 */
            PlaceQVertices(nextAlternatingLayer, _layers[nextLayerIndex], straightSweep);
            nextAlternatingLayer.SetPositions();

            /* 5 */
            int crossCount = DoCrossCountingAndOptimization(alternatingLayer, nextAlternatingLayer, straightSweep, enableSameMeasureOptimization, (phase == 2), prevCrossCount);

            /* 6 */
            nextAlternatingLayer.EnsureAlternatingAndPositions();

            alternatingLayer = nextAlternatingLayer;
            return crossCount;
        }

        private IList<SugiVertex> FindVerticesWithSameMeasure(AlternatingLayer nextAlternatingLayer, bool straightSweep, out IList<int> ranges, out int maxRangeLength)
        {
            var ignorableVertexType = straightSweep ? VertexTypes.QVertex : VertexTypes.PVertex;
            var verticesWithSameMeasure = new List<SugiVertex>();
            var vertices = nextAlternatingLayer.OfType<SugiVertex>().ToArray();
            int startIndex, endIndex;
            maxRangeLength = 0;
            ranges = new List<int>();
            for (startIndex = 0; startIndex < vertices.Length; startIndex = endIndex + 1)
            {
                for (endIndex = startIndex + 1;
                      endIndex < vertices.Length && vertices[startIndex].MeasuredPosition == vertices[endIndex].MeasuredPosition;
                      endIndex++) { }
                endIndex -= 1;

                if (endIndex > startIndex)
                {
                    int rangeLength = 0;
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        if (vertices[i].Type == ignorableVertexType || vertices[i].DoNotOpt)
                            continue;

                        rangeLength++;
                        verticesWithSameMeasure.Add(vertices[i]);
                    }
                    if (rangeLength > 0) 
                    {
                        maxRangeLength = Math.Max(rangeLength, maxRangeLength);
                        ranges.Add(rangeLength);
                    }
                }
            }
            return verticesWithSameMeasure;
        }

        private void AddAlternatingLayerToSparseCompactionGraph(AlternatingLayer nextAlternatingLayer, int layerIndex)
        {
            var sparseCompationGraphEdgesOfLayer = _sparseCompactionByLayerBackup[layerIndex];
            if (sparseCompationGraphEdgesOfLayer != null)
            {
                foreach (var edge in sparseCompationGraphEdgesOfLayer)
                    _sparseCompactionGraph.RemoveEdge(edge);
            }

            sparseCompationGraphEdgesOfLayer = new List<Edge<Data>>();
            SugiVertex prevVertex = null;
            for (int i = 1; i < nextAlternatingLayer.Count; i += 2)
            {
                var vertex = nextAlternatingLayer[i] as SugiVertex;
                var prevContainer = nextAlternatingLayer[i - 1] as SegmentContainer;
                var nextContainer = nextAlternatingLayer[i + 1] as SegmentContainer;
                if (prevContainer != null && prevContainer.Count > 0)
                {
                    var lastSegment = prevContainer[prevContainer.Count - 1];
                    var edge = new Edge<Data>(lastSegment, vertex);
                    sparseCompationGraphEdgesOfLayer.Add(edge);
                    _sparseCompactionGraph.AddVerticesAndEdge(edge);
                }
                else if (prevVertex != null)
                {
                    var edge = new Edge<Data>(prevVertex, vertex);
                    sparseCompationGraphEdgesOfLayer.Add(edge);
                    _sparseCompactionGraph.AddVerticesAndEdge(edge);
                }

                if (nextContainer != null && nextContainer.Count > 0)
                {
                    var firstSegment = nextContainer[0];
                    var edge = new Edge<Data>(vertex, firstSegment);
                    sparseCompationGraphEdgesOfLayer.Add(edge);
                    _sparseCompactionGraph.AddVerticesAndEdge(edge);
                }

                if (vertex != null && !_sparseCompactionGraph.ContainsVertex(vertex))
                    _sparseCompactionGraph.AddVertex(vertex);
                prevVertex = vertex;
            }
            _sparseCompactionByLayerBackup[layerIndex] = sparseCompationGraphEdgesOfLayer;
        }

        private class VertexGroup
        {
            public int Position;
            public int Size;
        }

        private class CrossCounterPair : Pair
        {
            public EdgeTypes Type = EdgeTypes.InnerSegment;
            public SugiEdge NonInnerSegment;
        }

        private class CrossCounterTreeNode
        {
            public int Accumulator;
            public bool InnerSegmentMarker;
            public readonly Queue<SugiEdge> NonInnerSegmentQueue = new Queue<SugiEdge>();
        }

        private int DoCrossCountingAndOptimization(
            AlternatingLayer alternatingLayer,
            AlternatingLayer nextAlternatingLayer,
            bool straightSweep,
            bool enableSameMeasureOptimization,
            bool reverseVerticesWithSameMeasure,
            int prevCrossCount)
        {
            IList<CrossCounterPair> realEdgePairs;
            var topLayer = straightSweep ? alternatingLayer : nextAlternatingLayer;
            var bottomLayer = straightSweep ? nextAlternatingLayer : alternatingLayer;

            var lastOnTopLayer = topLayer[topLayer.Count - 1];
            var lastOnBottomLayer = bottomLayer[bottomLayer.Count - 1];
            int firstLayerSize = lastOnTopLayer.Position + (lastOnTopLayer is ISegmentContainer ? ((ISegmentContainer)lastOnTopLayer).Count : 1);
            int secondLayerSize = lastOnBottomLayer.Position + (lastOnBottomLayer is ISegmentContainer ? ((ISegmentContainer)lastOnBottomLayer).Count : 1);

            IList<CrossCounterPair> virtualEdgePairs = FindVirtualEdgePairs(topLayer, bottomLayer);
            IList<SugiEdge> realEdges = FindRealEdges(topLayer);

            if (enableSameMeasureOptimization || reverseVerticesWithSameMeasure)
            {
                IList<int> ranges;
                int maxRangeLength;
                var verticesWithSameMeasure = FindVerticesWithSameMeasure(nextAlternatingLayer, straightSweep, out ranges, out maxRangeLength);
                var verticesWithSameMeasureSet = new HashSet<SugiVertex>(verticesWithSameMeasure);

                //initialize permutation indices
                for (int i = 0; i < verticesWithSameMeasure.Count; i++)
                    verticesWithSameMeasure[i].PermutationIndex = i;

                int bestCrossCount = prevCrossCount;
                foreach (var realEdge in realEdges)
                    realEdge.SaveMarkedToTemp();

                List<SugiVertex> sortedVertexList;
                if (!reverseVerticesWithSameMeasure)
                {
                    sortedVertexList = new List<SugiVertex>(verticesWithSameMeasure);
                }
                else
                {
                    sortedVertexList = new List<SugiVertex>(verticesWithSameMeasure.Count);
                    var stack = new Stack<SugiVertex>(verticesWithSameMeasure.Count);
                    var rnd = new Random(DateTime.Now.Millisecond);
                    foreach (var v in verticesWithSameMeasure)
                    {
                        if (stack.Count > 0 && (stack.Peek().MeasuredPosition != v.MeasuredPosition || rnd.NextDouble() > 0.8))
                        {
                            while (stack.Count > 0)
                                sortedVertexList.Add(stack.Pop());
                        }
                        stack.Push(v);
                    }
                    while (stack.Count > 0)
                    {
                        sortedVertexList.Add(stack.Pop());
                    }
                }

                int maxPermutations = EfficientSugiyamaLayoutParameters.MaxPermutations;
                do
                {
                    maxPermutations--;
                    if (!reverseVerticesWithSameMeasure)
                    {
                        //sort by permutation index and measure
                        sortedVertexList.Sort((v1, v2) =>
                        {
                            if (v1.MeasuredPosition != v2.MeasuredPosition)
                                return Math.Sign(v1.MeasuredPosition - v2.MeasuredPosition);

                            return v1.PermutationIndex - v2.PermutationIndex;
                        });
                    }

                    //reinsert the vertices into the layer
                    ReinsertVerticesIntoLayer(nextAlternatingLayer, verticesWithSameMeasureSet, sortedVertexList);

                    //set the positions
                    nextAlternatingLayer.SetPositions();
                    realEdgePairs = ConvertRealEdgesToCrossCounterPairs(realEdges, true);

                    var edgePairs = new List<CrossCounterPair>();
                    edgePairs.AddRange(virtualEdgePairs);
                    edgePairs.AddRange(realEdgePairs);

                    int crossCount = BiLayerCrossCount(edgePairs, firstLayerSize, secondLayerSize);

                    if (reverseVerticesWithSameMeasure)
                        return crossCount;

                    // if the crosscount is better than the best known save the actual state
                    if (crossCount < bestCrossCount)
                    {
                        foreach (var vertex in verticesWithSameMeasure)
                            vertex.SavePositionToTemp();

                        foreach (var edge in realEdges)
                            edge.SaveMarkedToTemp();

                        bestCrossCount = crossCount;
                    }
                    if (crossCount == 0)
                        break;
                } while (maxPermutations > 0 && Permutate(verticesWithSameMeasure, ranges));

                //reload the best solution
                foreach (var vertex in verticesWithSameMeasure)
                    vertex.LoadPositionFromTemp();

                foreach (var edge in realEdges)
                    edge.LoadMarkedFromTemp();

                //sort by permutation index and measure
                sortedVertexList.Sort((v1, v2) => v1.Position - v2.Position);

                //reinsert the vertices into the layer
                ReinsertVerticesIntoLayer(nextAlternatingLayer, verticesWithSameMeasureSet, sortedVertexList);
                nextAlternatingLayer.SetPositions();

                return bestCrossCount;
            }
            else
            {
                realEdgePairs = ConvertRealEdgesToCrossCounterPairs(realEdges, true);
                var edgePairs = new List<CrossCounterPair>();
                edgePairs.AddRange(virtualEdgePairs);
                edgePairs.AddRange(realEdgePairs);

                return BiLayerCrossCount(edgePairs, firstLayerSize, secondLayerSize);
            }
        }

        private static void ReinsertVerticesIntoLayer(
            AlternatingLayer layer,
            HashSet<SugiVertex> vertexSet,
            IList<SugiVertex> vertexList)
        {
            int reinsertIndex = 0;
            for (int i = 0; i < layer.Count; i++)
            {
                var vertex = layer[i] as SugiVertex;
                if (vertex == null || !vertexSet.Contains(vertex))
                    continue;

                layer.RemoveAt(i);
                layer.Insert(i, vertexList[reinsertIndex]);
                reinsertIndex++;
            }
        }

        private IList<CrossCounterPair> ConvertRealEdgesToCrossCounterPairs(IEnumerable<SugiEdge> edges, bool clearMark)
        {
            var pairs = new List<CrossCounterPair>();
            foreach (var edge in edges)
            {
                var source = edge.Source;
                var target = edge.Target;
                pairs.Add(
                    new CrossCounterPair
                    {
                        First = source.Position,
                        Second = target.Position,
                        Weight = 1,
                        Type = EdgeTypes.NonInnerSegment,
                        NonInnerSegment = edge
                    });

                if (clearMark)
                    edge.Marked = false;
            }
            return pairs;
        }

        private IList<SugiEdge> FindRealEdges(IEnumerable<IData> topLayer)
        {
            return topLayer.OfType<SugiVertex>().Where(vertex => vertex.Type != VertexTypes.PVertex).SelectMany(vertex => _graph.OutEdges(vertex)).ToList();
        }

        private static IList<CrossCounterPair> FindVirtualEdgePairs(IEnumerable<IData> topLayer, IEnumerable<IData> bottomLayer)
        {
            var virtualEdgePairs = new List<CrossCounterPair>();
            Queue<VertexGroup> firstLayerQueue = GetContainerLikeItems(topLayer, VertexTypes.PVertex);
            Queue<VertexGroup> secondLayerQueue = GetContainerLikeItems(bottomLayer, VertexTypes.QVertex);
            var vg1 = new VertexGroup();
            var vg2 = new VertexGroup();
            while (firstLayerQueue.Count > 0 || secondLayerQueue.Count > 0)
            {
                if (vg1.Size == 0)
                    vg1 = firstLayerQueue.Dequeue();
                if (vg2.Size == 0)
                    vg2 = secondLayerQueue.Dequeue();
                if (vg1.Size <= vg2.Size)
                {
                    virtualEdgePairs.Add(
                        new CrossCounterPair
                        {
                            First = vg1.Position,
                            Second = vg2.Position,
                            Weight = vg1.Size
                        });
                    vg2.Size -= vg1.Size;
                    vg1.Size = 0;
                }
                else
                {
                    virtualEdgePairs.Add(
                        new CrossCounterPair
                        {
                            First = vg1.Position,
                            Second = vg2.Position,
                            Weight = vg2.Size
                        });
                    vg1.Size -= vg2.Size;
                    vg2.Size = 0;
                }
            }
            return virtualEdgePairs;
        }

        private bool Permutate(IList<SugiVertex> vertices, IList<int> ranges) 
        {
            bool b = false;
            for (int i = 0, startIndex = 0; i < ranges.Count; startIndex += ranges[i], i++) {
                b = b || PermutateSomeHow(vertices, startIndex, ranges[i]);
            }
            return b;
        }

        private bool PermutateSomeHow(IList<SugiVertex> vertices, int startIndex, int count)
        {
            return count <= 4 ? Permutate(vertices, startIndex, count) : PermutateRandom(vertices, startIndex, count);
        }

        private bool PermutateRandom(IList<SugiVertex> vertices, int startIndex, int count) 
        {
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++) 
            {
                vertices[i].PermutationIndex = _rnd.Next(count);
            }
            return true;
        }

        private static bool Permutate(IList<SugiVertex> vertices, int startIndex, int count)
        {
            //do the initial ordering
            int n = startIndex + count;
            int i, j;

            //find place to start
            for (i = n - 1;
                  i > startIndex && vertices[i - 1].PermutationIndex >= vertices[i].PermutationIndex;
                  i--) { }

            //all in reverse order
            if (i < startIndex + 1)
                return false; //no more permutation

            //do next permutation
            for (j = n;
                  j > startIndex + 1 && vertices[j - 1].PermutationIndex <= vertices[i - 1].PermutationIndex;
                  j--) { }

            //swap values i-1, j-1
            int c = vertices[i - 1].PermutationIndex;
            vertices[i - 1].PermutationIndex = vertices[j - 1].PermutationIndex;
            vertices[j - 1].PermutationIndex = c;

            //need more swaps
            for (i++, j = n; i < j; i++, j--)
            {
                c = vertices[i - 1].PermutationIndex;
                vertices[i - 1].PermutationIndex = vertices[j - 1].PermutationIndex;
                vertices[j - 1].PermutationIndex = c;
            }

            return true; //new permutation generated
        }

        private static int BiLayerCrossCount(IEnumerable<CrossCounterPair> pairs, int firstLayerVertexCount, int secondLayerVertexCount)
        {
            if (pairs == null)
                return 0;

            //radix sort of the pair, order by First asc, Second asc

            #region Sort by Second ASC
            var radixBySecond = new List<CrossCounterPair>[secondLayerVertexCount];
            List<CrossCounterPair> r;
            int pairCount = 0;
            foreach (var pair in pairs)
            {
                //get the radix where the pair should be inserted
                r = radixBySecond[pair.Second];
                if (r == null)
                {
                    r = new List<CrossCounterPair>();
                    radixBySecond[pair.Second] = r;
                }
                r.Add(pair);
                pairCount = Math.Max(pairCount, pair.Second);
            }
            pairCount += 1;
            #endregion

            #region Sort By First ASC
            var radixByFirst = new List<CrossCounterPair>[firstLayerVertexCount];
            foreach (var list in radixBySecond)
            {
                if (list == null)
                    continue;

                foreach (var pair in list)
                {
                    //get the radix where the pair should be inserted
                    r = radixByFirst[pair.First];
                    if (r == null)
                    {
                        r = new List<CrossCounterPair>();
                        radixByFirst[pair.First] = r;
                    }
                    r.Add(pair);
                }
            }
            #endregion

            //
            // Build the accumulator tree
            //
            int firstIndex = 1;
            while (firstIndex < pairCount)
                firstIndex *= 2;
            int treeSize = 2 * firstIndex - 1;
            firstIndex -= 1;
            var tree = new CrossCounterTreeNode[treeSize];
            for (int i = 0; i < treeSize; i++)
                tree[i] = new CrossCounterTreeNode();

            //
            // Count the crossings
            //
            int crossCount = 0;
            foreach (var list in radixByFirst)
            {
                if (list == null)
                    continue;

                foreach (var pair in list)
                {
                    int index = pair.Second + firstIndex;
                    tree[index].Accumulator += pair.Weight;
                    switch (pair.Type)
                    {
                        case EdgeTypes.InnerSegment:
                            tree[index].InnerSegmentMarker = true;
                            break;
                        case EdgeTypes.NonInnerSegment:
                            tree[index].NonInnerSegmentQueue.Enqueue(pair.NonInnerSegment);
                            break;
                    }
                    while (index > 0)
                    {
                        if (index % 2 > 0)
                        {
                            crossCount += tree[index + 1].Accumulator * pair.Weight;
                            switch (pair.Type)
                            {
                                case EdgeTypes.InnerSegment:
                                    var queue = tree[index + 1].NonInnerSegmentQueue;
                                    while (queue.Count > 0)
                                    {
                                        queue.Dequeue().Marked = true;
                                    }
                                    break;
                                case EdgeTypes.NonInnerSegment:
                                    if (tree[index + 1].InnerSegmentMarker)
                                    {
                                        pair.NonInnerSegment.Marked = true;
                                    }
                                    break;
                            }
                        }
                        index = (index - 1) / 2;
                        tree[index].Accumulator += pair.Weight;
                        switch (pair.Type)
                        {
                            case EdgeTypes.InnerSegment:
                                tree[index].InnerSegmentMarker = true;
                                break;
                            case EdgeTypes.NonInnerSegment:
                                tree[index].NonInnerSegmentQueue.Enqueue(pair.NonInnerSegment);
                                break;
                        }
                    }
                }
            }

            return crossCount;
        }

        private static Queue<VertexGroup> GetContainerLikeItems(IEnumerable<IData> alternatingLayer, VertexTypes containerLikeVertexType)
        {
            var queue = new Queue<VertexGroup>();
            foreach (var item in alternatingLayer)
            {
                var vertex = item as SugiVertex;
                if (vertex != null && vertex.Type == containerLikeVertexType)
                {
                    queue.Enqueue(new VertexGroup { Position = vertex.Position, Size = 1 });
                }
                else if (vertex == null)
                {
                    var container = item as ISegmentContainer;
                    if (container != null && container.Count > 0)
                        queue.Enqueue(new VertexGroup { Position = container.Position, Size = container.Count });
                }
            }
            return queue;
        }

        private static void PlaceQVertices(AlternatingLayer alternatingLayer, IEnumerable<SugiVertex> nextLayer, bool straightSweep)
        {
            var type = straightSweep ? VertexTypes.QVertex : VertexTypes.PVertex;
            var qVertices = new HashSet<SugiVertex>();
            foreach (var vertex in nextLayer)
            {
                if (vertex.Type != type)
                    continue;

                qVertices.Add(vertex);
            }

            for (int i = 0; i < alternatingLayer.Count; i++)
            {
                var segmentContainer = alternatingLayer[i] as SegmentContainer;
                if (segmentContainer == null)
                    continue;
                for (int j = 0; j < segmentContainer.Count; j++)
                {
                    var segment = segmentContainer[j];
                    var vertex = straightSweep ? segment.QVertex : segment.PVertex;
                    if (!qVertices.Contains(vertex))
                        continue;

                    alternatingLayer.RemoveAt(i);
                    ISegmentContainer sc1, sc2;
                    segmentContainer.Split(segment, out sc1, out sc2);
                    sc1.Position = segmentContainer.Position;
                    sc2.Position = segmentContainer.Position + sc1.Count + 1;
                    alternatingLayer.Insert(i, sc1);
                    alternatingLayer.Insert(i + 1, vertex);
                    alternatingLayer.Insert(i + 2, sc2);
                    i = i + 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Replaces the P or Q vertices of the actualLayer with their
        /// segment on the next layer.
        /// </summary>
        /// <param name="alternatingLayer">The actual alternating layer. It will be modified.</param>
        /// <param name="straightSweep">If true, we are sweeping down else we're sweeping up.</param>
        private static void AppendSegmentsToAlternatingLayer(AlternatingLayer alternatingLayer, bool straightSweep)
        {
            var type = straightSweep ? VertexTypes.PVertex : VertexTypes.QVertex;
            for (int i = 1; i < alternatingLayer.Count; i += 2)
            {
                var vertex = (SugiVertex)alternatingLayer[i];
                if (vertex.Type == type)
                {
                    var precedingContainer = (SegmentContainer)alternatingLayer[i - 1];
                    var succeedingContainer = (SegmentContainer)alternatingLayer[i + 1];
                    precedingContainer.Append(vertex.Segment);
                    precedingContainer.Join(succeedingContainer);

                    //remove the vertex and the succeeding container from the alternating layer
                    alternatingLayer.RemoveRange(i, 2);
                    i -= 2;
                }
            }
        }

        private void ComputeMeasureValues(AlternatingLayer alternatingLayer, int nextLayerIndex, bool straightSweep)
        {
            AssignPositionsOnActualLayer(alternatingLayer);
            AssignMeasuresOnNextLayer(_layers[nextLayerIndex], straightSweep);
        }

        private static AlternatingLayer InitialOrderingOfNextLayer(IEnumerable<IData> alternatingLayer, IEnumerable<SugiVertex> nextLayer, bool straightSweep)
        {
            //get the list of the containers and vertices
            var segmentContainerStack = new Stack<ISegmentContainer>(alternatingLayer.OfType<ISegmentContainer>().Reverse());
            var ignorableVertexType = straightSweep ? VertexTypes.QVertex : VertexTypes.PVertex;
            var vertexStack = new Stack<SugiVertex>(nextLayer.Where(v => v.Type != ignorableVertexType).OrderBy(v => v.MeasuredPosition).Reverse());
            var newAlternatingLayer = new AlternatingLayer();

            while (vertexStack.Count > 0 && segmentContainerStack.Count > 0)
            {
                var vertex = vertexStack.Peek();
                var segmentContainer = segmentContainerStack.Peek();
                if (vertex.MeasuredPosition <= segmentContainer.Position)
                {
                    newAlternatingLayer.Add(vertexStack.Pop());
                }
                else if (vertex.MeasuredPosition >= (segmentContainer.Position + segmentContainer.Count - 1))
                {
                    newAlternatingLayer.Add(segmentContainerStack.Pop());
                }
                else
                {
                    vertexStack.Pop();
                    segmentContainerStack.Pop();
                    var k = (int)Math.Ceiling(vertex.MeasuredPosition - segmentContainer.Position);
                    ISegmentContainer sc1, sc2;
                    segmentContainer.Split(k, out sc1, out sc2);
                    newAlternatingLayer.Add(sc1);
                    newAlternatingLayer.Add(vertex);
                    sc2.Position = segmentContainer.Position + k;
                    segmentContainerStack.Push(sc2);
                }
            }
            if (vertexStack.Count > 0)
                newAlternatingLayer.AddRange(vertexStack.OfType<IData>());
            if (segmentContainerStack.Count > 0)
                newAlternatingLayer.AddRange(segmentContainerStack.OfType<IData>());

            return newAlternatingLayer;
        }

        /// <summary>
        /// Assigns the positions of the vertices and segment container 
        /// on the actual layer.
        /// </summary>
        /// <param name="alternatingLayer">The actual layer (L_i).</param>
        private static void AssignPositionsOnActualLayer(AlternatingLayer alternatingLayer)
        {
            //assign positions to vertices on the actualLayer (L_i)
            for (int i = 1; i < alternatingLayer.Count; i += 2)
            {
                var precedingContainer = (SegmentContainer)alternatingLayer[i - 1];
                var vertex = alternatingLayer[i];
                if (i == 1)
                {
                    vertex.Position = precedingContainer.Count;
                }
                else
                {
                    var previousVertex = alternatingLayer[i - 2];
                    vertex.Position = previousVertex.Position + precedingContainer.Count + 1;
                }
            }

            //assign positions to containers on the actualLayer (L_i+1)
            for (int i = 0; i < alternatingLayer.Count; i += 2)
            {
                var container = (SegmentContainer)alternatingLayer[i];
                if (i == 0)
                {
                    container.Position = 0;
                }
                else
                {
                    var precedingVertex = alternatingLayer[i - 1];
                    container.Position = precedingVertex.Position + 1;
                }
            }
        }

        private void AssignMeasuresOnNextLayer(IEnumerable<SugiVertex> layer, bool straightSweep)
        {
            //measures of the containers is the same as their positions
            //so we should set the measures only for the vertices
            foreach (var vertex in layer)
            {
                if ((straightSweep && vertex.Type == VertexTypes.QVertex)
                    || (!straightSweep && vertex.Type == VertexTypes.PVertex))
                    continue;

                var edges = straightSweep ? _graph.InEdges(vertex) : _graph.OutEdges(vertex);
                var oldMeasuredPosition = vertex.MeasuredPosition;
                vertex.MeasuredPosition = 0;
                vertex.DoNotOpt = false;
                int count = 0;
                foreach (var edge in edges)
                {
                    var otherVertex = edge.OtherVertex(vertex);
                    vertex.MeasuredPosition += otherVertex.Position;
                    count++;
                }
                if (count > 0)
                    vertex.MeasuredPosition /= count;
                else
                {
                    vertex.MeasuredPosition = oldMeasuredPosition;
                    vertex.DoNotOpt = true;
                }
            }
        }
    }
}

