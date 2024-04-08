using System;
using System.Collections.Generic;
using System.Windows;

namespace GraphSharp.Algorithms.OverlapRemoval
{
    public class OneWayFSAAlgorithm<TObject> : FSAAlgorithm<TObject, OneWayFSAParameters>
        where TObject : class
    {
        public OneWayFSAAlgorithm( IDictionary<TObject, Rect> rectangles, OneWayFSAParameters parameters )
            : base( rectangles, parameters )
        {
        }

        protected override void RemoveOverlap()
        {
            switch ( Parameters.Way )
            {
                case OneWayFSAWayEnum.Horizontal:
                    HorizontalImproved();
                    break;
                case OneWayFSAWayEnum.Vertical:
                    VerticalImproved();
                    break;
            }
        }

        protected new double HorizontalImproved()
        {
            WrappedRectangles.Sort( XComparison );
            int i = 0, n = WrappedRectangles.Count;

            var lmin = WrappedRectangles[0];
            double sigma = 0, x0 = lmin.CenterX;
            var gamma = new double[WrappedRectangles.Count];
            var x = new double[WrappedRectangles.Count];
            while ( i < n )
            {
                var u = WrappedRectangles[i];

                int k = i;
                for ( int j = i + 1; j < n; j++ )
                {
                    var v = WrappedRectangles[j];
                    if ( u.CenterX == v.CenterX )
                    {
                        u = v;
                        k = j;
                    }
                    else
                    {
                        break;
                    }
                }
                double g = 0;

                for ( int z = i + 1; z <= k; z++ )
                {
                    var v = WrappedRectangles[z];
                    v.Rectangle.X += ( z - i ) * 0.0001;
                }

                if ( u.CenterX > x0 )
                {
                    for ( int m = i; m <= k; m++ )
                    {
                        double ggg = 0;
                        for ( int j = 0; j < i; j++ )
                        {
                            var f = Force( WrappedRectangles[j].Rectangle, WrappedRectangles[m].Rectangle );
                            ggg = Math.Max( f.X + gamma[j], ggg );
                        }
                        var v = WrappedRectangles[m];
                        double gg = v.Rectangle.Left + ggg < lmin.Rectangle.Left ? sigma : ggg;
                        g = Math.Max( g, gg );
                    }
                }

                for ( int m = i; m <= k; m++ )
                {
                    gamma[m] = g;
                    var r = WrappedRectangles[m];
                    x[m] = r.Rectangle.Left + g;
                    if ( r.Rectangle.Left < lmin.Rectangle.Left )
                    {
                        lmin = r;
                    }
                }

                double delta = 0;
                for ( int m = i; m <= k; m++ )
                {
                    for ( int j = k + 1; j < n; j++ )
                    {
                        var f = Force( WrappedRectangles[m].Rectangle, WrappedRectangles[j].Rectangle );
                        if ( f.X > delta )
                        {
                            delta = f.X;
                        }
                    }
                }
                sigma += delta;
                i = k + 1;
            }
            double cost = 0;
            for ( i = 0; i < n; i++ )
            {
                var r = WrappedRectangles[i];
                double oldPos = r.Rectangle.Left;
                double newPos = x[i];

                r.Rectangle.X = newPos;

                double diff = oldPos - newPos;
                cost += diff * diff;
            }
            return cost;
        }
    }
}