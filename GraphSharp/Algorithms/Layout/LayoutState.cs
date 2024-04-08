using System;
using System.Collections.Generic;
using System.Windows;

namespace GraphSharp.Algorithms.Layout
{
	public class LayoutState<TVertex, TEdge>
	{
		/// <summary>Gets the position of every vertex in this state of the layout process.</summary>
		public IDictionary<TVertex, Point> Positions { get; protected set; }

		public IDictionary<TVertex, Point> OverlapRemovedPositions { get; set; }

		public IDictionary<TEdge, Point[]> RouteInfos { get; set; }

		/// <summary>Gets how much time did it take to compute the position of the vertices (till the end of this iteration).</summary>
		public TimeSpan ComputationTime { get; protected set; }

		/// <summary>Gets the index of the iteration.</summary>
		public int Iteration { get; protected set; }

		/// <summary>Get the status message of this layout state.</summary>
		public string Message { get; protected set; }

	    public LayoutState(IDictionary<TVertex, Point> positions, IDictionary<TVertex, Point> overlapRemovedPositions, IDictionary<TEdge, Point[]> routeInfos, TimeSpan computationTime,
	                       int iteration, string message)
		{
			Positions = positions;
			OverlapRemovedPositions = overlapRemovedPositions ?? positions;
			RouteInfos = routeInfos ?? new Dictionary<TEdge, Point[]>( 0 );
			ComputationTime = computationTime;
			Iteration = iteration;
			Message = message;
		}
	}
}