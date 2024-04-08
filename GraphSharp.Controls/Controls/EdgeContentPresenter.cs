using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphSharp.Controls
{
	public class EdgeContentPresenter : ContentPresenter
	{
		public EdgeContentPresenter()
		{
			LayoutUpdated += new EventHandler(EdgeContentPresenter_LayoutUpdated);
		}

		private EdgeControl GetEdgeControl(DependencyObject parent)
		{
			while (parent != null)
				if (parent is EdgeControl)
					return (EdgeControl)parent;
				else
					parent = VisualTreeHelper.GetParent(parent);
			return null;
		}

		private static double GetAngleBetweenPoints(Point point1, Point point2)
		{
			return Math.Atan2(point1.Y - point2.Y, point2.X - point1.X);
		}

		private static double GetDistanceBetweenPoints(Point point1, Point point2)
		{
			return Math.Sqrt(Math.Pow(point2.X - point1.X, 2) + Math.Pow(point2.Y - point1.Y, 2));
		}

		private static double GetLabelDistance(double edgeLength)
		{
			return edgeLength / 2;  // set the label halfway the length of the edge
		}

		void EdgeContentPresenter_LayoutUpdated(object sender, EventArgs e)
		{
			if (!IsLoaded)
				return;
			var edgeControl = GetEdgeControl(VisualParent);
			if (edgeControl == null)
				return;
			var source = edgeControl.Source;
			var p1 = new Point(GraphCanvas.GetX(source), GraphCanvas.GetY(source));
			var target = edgeControl.Target;
			var p2 = new Point(GraphCanvas.GetX(target), GraphCanvas.GetY(target));

			double edgeLength;
			var routePoints = edgeControl.RoutePoints;
			if (routePoints == null)
				// the edge is a single segment (p1,p2)
				edgeLength = GetLabelDistance(GetDistanceBetweenPoints(p1, p2));
			else
			{
				// the edge has one or more segments
				// compute the total length of all the segments
				edgeLength = 0;
				for (int i = 0; i <= routePoints.Length; ++i)
					if (i == 0)
						edgeLength += GetDistanceBetweenPoints(p1, routePoints[0]);
					else if (i == routePoints.Length)
						edgeLength += GetDistanceBetweenPoints(routePoints[routePoints.Length - 1], p2);
					else
						edgeLength += GetDistanceBetweenPoints(routePoints[i - 1], routePoints[i]);
				// find the line segment where the half distance is located
				edgeLength = GetLabelDistance(edgeLength);
				Point newp1 = p1;
				Point newp2 = p2;
				for (int i = 0; i <= routePoints.Length; ++i)
				{
					double lengthOfSegment;
					if (i == 0)
						lengthOfSegment = GetDistanceBetweenPoints(newp1 = p1, newp2 = routePoints[0]);
					else if (i == routePoints.Length)
						lengthOfSegment = GetDistanceBetweenPoints(newp1 = routePoints[routePoints.Length - 1], newp2 = p2);
					else
						lengthOfSegment = GetDistanceBetweenPoints(newp1 = routePoints[i - 1], newp2 = routePoints[i]);
					if (lengthOfSegment >= edgeLength)
						break;
					edgeLength -= lengthOfSegment;
				}
				// redefine our edge points
				p1 = newp1;
				p2 = newp2;
			}
			// align the point so that it  passes through the center of the label content
			var p = p1;
			var desiredSize = DesiredSize;
			p.Offset(-desiredSize.Width / 2, -desiredSize.Height / 2);

			// move it "edgLength" on the segment
			var angleBetweenPoints = GetAngleBetweenPoints(p1, p2);
			//p.Offset(edgeLength * Math.Cos(angleBetweenPoints), -edgeLength * Math.Sin(angleBetweenPoints));
			float x = 12.5f, y = 12.5f;
			double sin = Math.Sin(angleBetweenPoints);
			double cos = Math.Cos(angleBetweenPoints);
			double sign = sin * cos / Math.Abs(sin * cos);
			p.Offset(x * sin * sign + edgeLength * cos, y * cos * sign - edgeLength * sin);
			Arrange(new Rect(p, desiredSize));
		}
	}
}
