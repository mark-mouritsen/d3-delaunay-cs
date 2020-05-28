using System;
using System.Collections.Generic;
using System.Linq;
using static d3_delaunay_cs.Delaunator;
using static d3_delaunay_cs.Voronoi;

namespace d3_delaunay_cs
{
    public class Delaunay
    {
        const double tau = 2 * Math.PI;

        public Delaunator delaunator;

        public double[] points;
        public uint[] triangles;
        public int[] halfedges;
        public Node hull;

        public int[] inedges;
        public int[] outedges;

        static Func<double[], int, double[][], double> pointX = 
            (double[] p, int i, double[][] points) => p[0];
        static Func<double[], int, double[][], double> pointY = 
            (double[] p, int i, double[][] points) => p[1];

        public static Delaunay from(
            double[][] points = null, 
            Func<double[], int, double[][], double> fx = null, 
            Func<double[], int, double[][], double> fy = null)
        {
            fx = fx ?? pointX;
            fy = fy ?? pointY;
            double[] returnPoints;
            if (points != null)
            {
                returnPoints = flatArray(points, fx, fy);
            }
            else
            {
                returnPoints = flatIterable(points, fx, fy).ToArray();
            }
            return new Delaunay(returnPoints);
        }

        public static double[] flatArray(
            double[][] points, 
            Func<double[], int, double[][], double> fx, 
            Func<double[], int, double[][], double> fy)
        {
            var n = points.Length;
            var array = new double[n * 2];
            for (var i = 0; i < n; ++i)
            {
                var p = points[i];
                array[i * 2] = fx.Invoke(p, i, points);
                array[i * 2 + 1] = fy.Invoke(p, i, points);
            }
            return array.ToArray();
        }

        public static IEnumerable<double> flatIterable(
            double[][] points,
            Func<double[], int, double[][], double> fx,
            Func<double[], int, double[][], double> fy)
        {
            var i = 0;
            foreach (var p in points) {
                yield return fx.Invoke(p, i, points);
                yield return fy.Invoke(p, i, points);
                ++i;
            }
        }

        public Delaunay(double[] points)
        {
            var delaunator = new Delaunator(points);
            this.delaunator = delaunator;
            var halfedges = delaunator.halfedges;
            var hull = delaunator.hull;
            var triangles = delaunator.triangles;

            this.points = points;
            this.halfedges = halfedges;
            this.hull = hull;
            this.triangles = triangles;
            var inedges = this.inedges = new int[points.Length / 2].Populate(-1);
            var outedges = this.outedges = new int[points.Length / 2].Populate(-1);

            // Compute an index from each point to an (arbitrary) incoming halfedge.
            for (int e = 0, n = halfedges.Length; e < n; ++e)
            {
                inedges[triangles[e % 3 == 2 ? e - 2 : e + 1]] = e;
            }

            // For points on the hull, index both the incoming and outgoing halfedges.
            Node node0, node1 = hull;
            do
            {
                node0 = node1;
                node1 = node1.next;
                inedges[node1.i] = node0.t;
                outedges[node0.i] = node1.t;
            } while (node1 != hull);
        }

        public Voronoi voronoi(Bounds bounds)
        {
            return new Voronoi(this, bounds);
        }

        public IEnumerable<uint> neighbors(int i)
        {
            var indedges = this.inedges;
            var outedges = this.outedges;
            var halfedges = this.halfedges;
            var triangles = this.triangles;
            var e0 = inedges[i];
            if (e0 == -1) yield break; // coincident point
            var e = e0;
            do
            {
                yield return e < 0 ? 0 : triangles[e];
                e = e % 3 == 2 ? e - 2 : e + 1;
                if (triangles[e] != i) yield break; // bad triangulation
                e = halfedges[e];
                if (e == -1) yield return triangles[outedges[i]];
            } while (e != e0);
        }

        public int find(double x, double y, int i = 0)
        {
            int c;
            c = this._step(i, x, y);
            while (c >= 0 && c != i)
            {
                i = c;
                c = this._step(i, x, y);
            }
            //while ((c = this._step(i, x, y)) >= 0 && c != i) i = c;
            return c;
        }

        public int _step(int i, double x, double y)
        {
            var inedges = this.inedges;
            var points = this.points;
            if (inedges[i] == -1) return -1; // coincident point
            var c = i;
            var dc = Math.Pow(x - points[i * 2], 2) + Math.Pow(y - points[i * 2 + 1], 2);
            foreach (var t in this.neighbors(i)) {
                var dt = Math.Pow(x - points[t * 2], 2) + Math.Pow(y - points[t * 2 + 1], 2);
                if (dt < dc)
                {
                    dc = dt;
                    c = (int)t;
                }
            }
            return c;
        }

        public string render(Path context)
        {
            var buffer = context == null ? context = new Path() : null;
            var points = this.points;
            var halfedges = this.halfedges;
            var triangles = this.triangles;
            for (int i = 0, n = halfedges.Length; i < n; ++i)
            {
                var j = halfedges[i];
                if (j < i) continue;
                var ti = triangles[i] * 2;
                var tj = triangles[j] * 2;
                context.moveTo(points[ti], points[ti + 1]);
                context.lineTo(points[tj], points[tj + 1]);
            }
            this.renderHull(context);
            return buffer?.value() ?? null;
        }

        public string renderPoints(Path context, int r = 2)
        {
            var buffer = context == null ? context = new Path() : null;
            var points = this.points;
            for (int i = 0, n = points.Length; i < n; i += 2)
            {
                var x = points[i];
                var y = points[i + 1];
                context.moveTo(x + r, y);
                context.arc(x, y, r); // TODO ??? , 0, tau);
            }
            return buffer?.value() ?? null;
        }

        public string renderHull(Path context)
        {
            var buffer = context == null ? context = new Path() : null;
            var hull = this.hull;
            context.moveTo(hull.x, hull.y);
            for (var node = hull.next; node != hull; node = node.next)
            {
                context.lineTo(node.x, node.y);
            }
            context.closePath();
            return buffer?.value() ?? null;
        }

        public List<List<double>> renderHull(Polygon context)
        {
            var buffer = context == null ? context = new Polygon() : null;
            var hull = this.hull;
            context.moveTo(hull.x, hull.y);
            for (var node = hull.next; node != hull; node = node.next)
            {
                context.lineTo(node.x, node.y);
            }
            context.closePath();
            return buffer?.value() ?? null;
        }

        public List<List<double>> hullPolygon()
        {
            var polygon = new Polygon();
            this.renderHull(polygon);
            return polygon.value();
        }

        public List<List<double>> renderTriangle(int i, Polygon context)
        {
            var buffer = context == null ? context = new Polygon() : null;
            var points = this.points;
            var triangles = this.triangles;
            var t0 = triangles[i *= 3] * 2;
            var t1 = triangles[i + 1] * 2;
            var t2 = triangles[i + 2] * 2;
            context.moveTo(points[t0], points[t0 + 1]);
            context.lineTo(points[t1], points[t1 + 1]);
            context.lineTo(points[t2], points[t2 + 1]);
            context.closePath();
            return buffer?.value() ?? null;
        }

        public IEnumerable<List<List<double>>> trianglePolygons()
        {
            var triangles = this.triangles;
            for (int i = 0, n = triangles.Length / 3; i < n; ++i)
            {
                yield return this.trianglePolygon(i);
            }
        }

        public List<List<double>> trianglePolygon(int i)
        {
            var polygon = new Polygon();
            this.renderTriangle(i, polygon);
            return polygon.value();
        }
    }

    static class ArrayExtensions
    {
        public static T[] Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }

            return arr;
        }
    }
}
