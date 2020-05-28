using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace d3_delaunay_cs
{
    public class Voronoi
    {
        Delaunay delaunay;

        double[] circumcenters;
        double[] vectors;

        double xmin, ymin, xmax, ymax;

        public Voronoi(Delaunay delaunay, Bounds bounds = null)
        {
            var xmin = bounds?.x0 ?? 0;
            var ymin = bounds?.y0 ?? 0;
            var xmax = bounds?.x1 ?? 960;
            var ymax = bounds?.y1 ?? 500;
            if (!(xmax >= xmin) || !(ymax >= ymin)) throw new Exception("invalid bounds");
            
            this.delaunay = delaunay;
            var points = this.delaunay.points;
            var hull = this.delaunay.hull;
            var triangles = this.delaunay.triangles;

            var circumcenters = this.circumcenters = new double[triangles.Length / 3 * 2];
            var vectors = this.vectors = new double[points.Length * 2];
            this.xmax = xmax;
            this.xmin = xmin;
            this.ymax = ymax;
            this.ymin = ymin;

            // Compute circumcenters.
            for (int i = 0, j = 0, n = triangles.Length; i < n; i += 3, j += 2)
            {
                var t1 = triangles[i] * 2;
                var t2 = triangles[i + 1] * 2;
                var t3 = triangles[i + 2] * 2;
                var X1 = points[t1];
                var Y1 = points[t1 + 1];
                var x2 = points[t2];
                var y2 = points[t2 + 1];
                var x3 = points[t3];
                var y3 = points[t3 + 1];
                var a2 = X1 - x2;
                var a3 = X1 - x3;
                var b2 = Y1 - y2;
                var b3 = Y1 - y3;
                var d1 = X1 * X1 + Y1 * Y1;
                var d2 = d1 - x2 * x2 - y2 * y2;
                var d3 = d1 - x3 * x3 - y3 * y3;
                var ab = (a3 * b2 - a2 * b3) * 2;
                circumcenters[j] = (b2 * d3 - b3 * d2) / ab;
                circumcenters[j + 1] = (a3 * d2 - a2 * d3) / ab;
            }

            // Compute exterior cell rays.
            var node = hull;
            uint p0, p1 = node.i * 4;
            double x0, x1 = node.x;
            double y0, y1 = node.y;
            do
            {
                node = node.next;
                p0 = p1;
                x0 = x1;
                y0 = y1;
                p1 = node.i * 4;
                x1 = node.x;
                y1 = node.y;
                vectors[p0 + 2] = vectors[p1] = y0 - y1;
                vectors[p0 + 3] = vectors[p1 + 1] = x1 - x0;
            } while (node != hull);
        }

        public string render(Path context)
        {
            var buffer = context == null ? context = new Path() : null;
            var halfedges = this.delaunay.halfedges;
            var hull = this.delaunay.hull;
            var circumcenters = this.circumcenters;
            var vectors = this.vectors;

            for (int i = 0, n = halfedges.Length; i < n; ++i)
            {
                var j = halfedges[i];
                if (j < i) continue;
                var ti = (int)Math.Floor((double)i / 3) * 2;
                var tj = (int)Math.Floor((double)j / 3) * 2;
                var xi = circumcenters[ti];
                var yi = circumcenters[ti + 1];
                var xj = circumcenters[tj];
                var yj = circumcenters[tj + 1];
                this._renderSegment(xi, yi, xj, yj, context);
            }
            var node = hull;
            do
            {
                node = node.next;
                var t = (int)Math.Floor((double)node.t / 3) * 2;
                var x = circumcenters[t];
                var y = circumcenters[t + 1];
                var v = node.i * 4;
                var p = this._project(x, y, vectors[v + 2], vectors[v + 3]);
                if (p != null) this._renderSegment(x, y, p[0], p[1], context);
            } while (node != hull);
            return buffer?.value() ?? null;
        }

        public string renderBounds(Path context)
        {
            var buffer = context == null ? context = new Path() : null;
            context.rect(this.xmin, this.ymin, this.xmax - this.xmin, this.ymax - this.ymin);
            return buffer?.value() ?? null;
        }

        public string renderCell(int i, Path context)
        {
            var buffer = context == null ? context = new Path() : null;
            var points = this._clip(i);
            if (points == null) return null;
            context.moveTo(points[0], points[1]);
            for (int ii = 2, n = points.Length; ii < n; ii += 2)
            {
                context.lineTo(points[ii], points[ii + 1]);
            }
            context.closePath();
            return buffer?.value() ?? null;
        }

        public List<List<double>> renderCell(int i, Polygon context)
        {
            var buffer = context == null ? context = new Polygon() : null;
            var points = this._clip(i);
            if (points == null) return null;
            context.moveTo(points[0], points[1]);
            for (int ii = 2, n = points.Length; ii < n; ii += 2)
            {
                context.lineTo(points[ii], points[ii + 1]);
            }
            context.closePath();
            return buffer?.value() ?? null;
        }

        public IEnumerable<List<List<double>>> cellPolygons()
        {
            var points = delaunay.points;
            for (int i = 0, n = points.Length / 2; i < n; ++i)
            {
                var cell = this.cellPolygon(i);
                if (cell != null) yield return cell;
            }
        }

        public List<List<double>> cellPolygon(int i)
        {
            var polygon = new Polygon();
            this.renderCell(i, polygon);
            return polygon.value();
        }

        private void _renderSegment(double x0, double y0, double x1, double y1, Path context)
        {
            double[] S;
            var c0 = this._regioncode(x0, y0);
            var c1 = this._regioncode(x1, y1);
            if (c0 == 0 && c1 == 0)
            {
                context.moveTo(x0, y0);
                context.lineTo(x1, y1);
            }
            else
            {
                S = this._clipSegment(x0, y0, x1, y1, c0, c1);
                if (S != null)
                {
                    context.moveTo(S[0], S[1]);
                    context.lineTo(S[2], S[3]);
                }
            }
        }

        private bool contains(int i, double x, double y)
        {
            return this.delaunay._step(i, x, y) == i;
        }

        private double[] _cell(int i)
        {
            var circumcenters = this.circumcenters;
            var inedges = delaunay.inedges;
            var halfedges = delaunay.halfedges;
            var triangles = delaunay.triangles;
            var e0 = inedges[i];
            if (e0 == -1) return null; // coincident point
            var points = new List<double>();
            var e = e0;
            do
            {
                var t = (int)Math.Floor((double)e / 3);
                points.Add(circumcenters[t * 2]);
                points.Add(circumcenters[t * 2 + 1]);
                e = e % 3 == 2 ? e - 2 : e + 1;
                if (triangles[e] != i) break; // bad triangulation
                e = halfedges[e];
            } while (e != e0 && e != -1);
            return points.ToArray();
        }

        private double[] _clip(int i)
        {
            var points = this._cell(i);
            if (points == null) return null;
            var V = vectors;
            var v = i * 4;

            return V[v] != 0 || V[v + 1] != 0
                ? this._clipInfinite(i, points, V[v], V[v + 1], V[v + 2], V[v + 3])
                : this._clipFinite(i, points);
        }

        private double[] _clipFinite(int i, double[] points)
        {
            var n = points.Length;
            List<double> P = null;
            double x0, y0, x1 = points[n - 2]; 
            var y1 = points[n - 1];
            int c0 = 0, c1 = this._regioncode(x1, y1);
            int e0 = 0, e1 = 0;
            for (var j = 0; j < n; j += 2)
            {
                x0 = x1;
                y0 = y1;
                x1 = points[j];
                y1 = points[j + 1];
                c0 = c1;
                c1 = this._regioncode(x1, y1);
                if (c0 == 0 && c1 == 0)
                {
                    e0 = e1;
                    e1 = 0;
                    if (P != null)
                    {
                        P.Add(x1);
                        P.Add(y1);
                    }
                    else P = new List<double> { x1, y1 };
                }
                else
                {
                    double[] S;
                    double sx0, sy0, sx1, sy1;
                    if (c0 == 0)
                    {
                        S = this._clipSegment(x0, y0, x1, y1, c0, c1);
                        if (S == null) continue;
                        sx0 = S[0];
                        sy0 = S[1];
                        sx1 = S[2];
                        sy1 = S[3];
                    }
                    else
                    {
                        S = this._clipSegment(x1, y1, x0, y0, c1, c0);
                        if (S == null) continue;
                        sx1 = S[0];
                        sy1 = S[1];
                        sx0 = S[2];
                        sy0 = S[3];
                        e0 = e1;
                        e1 = this._edgecode(sx0, sy0);
                        if (e0 != 0 && e1 != 0) this._edge(i, e0, e1, P, P.Count());
                        if (P != null)
                        {
                            P.Add(sx0);
                            P.Add(sy0);
                        }
                        else P = new List<double> { sx0, sy0 };
                    }
                    e0 = e1;
                    e1 = this._edgecode(sx1, sy1);
                    if (e0 != 0 && e1 != 0) this._edge(i, e0, e1, P, P.Count());
                    if (P != null)
                    {
                        P.Add(sx1);
                        P.Add(sy1);
                    }
                    else P = new List<double> { sx1, sy1 };
                }
            }
            if (P != null)
            {
                e0 = e1;
                e1 = this._edgecode(P[0], P[1]);
                if (e0 != 0 && e1 != 0) this._edge(i, e0, e1, P, P.Count());
            }
            else if (this.contains(i, (this.xmin + this.xmax) / 2, (this.ymin + this.ymax) / 2))
            {
                return new double[] { this.xmax, this.ymin, this.xmax, this.ymax, this.xmin, this.ymax, this.xmin, this.ymin };
            }
            return P.ToArray();
        }

        public double[] _clipInfinite(int i, double[] points, double vx0, double vy0, double vxn, double vyn)
        {
            var P = points.ToList();
            double[] p;
            p = this._project(P[0], P[1], vx0, vy0);
            if (p != null)
            {
                P.Prepend(p[1]);
                P.Prepend(p[0]);
            }

            p = this._project(P[P.Count() - 2], P[P.Count() - 1], vxn, vyn);
            if (p != null)
            {
                P.Add(p[0]);
                P.Add(p[1]);
            }

            P = this._clipFinite(i, P.ToArray()).ToList();
            if (P != null)
            {
                for (int j = 0, n = P.Count(), c0, c1 = this._edgecode(P[n - 2], P[n - 1]); j < n; j += 2)
                {
                    c0 = c1;
                    c1 = this._edgecode(P[j], P[j + 1]);
                    if (c0 != 0 && c1 != 0) j = this._edge(i, c0, c1, P, j);
                    n = P.Count();
                }
            }
            else if (this.contains(i, (this.xmin + this.xmax) / 2, (this.ymin + this.ymax) / 2))
            {
                P = new List<double> { this.xmin, this.ymin, this.xmax, this.ymin, this.xmax, this.ymax, this.xmin, this.ymax };
            }
            return P.ToArray();
        }

        private double[] _clipSegment(double x0, double y0, double x1, double y1, int c0, int c1)
        {
            while (true)
            {
                if (c0 == 0 && c1 == 0) return new double[] { x0, y0, x1, y1 };
                if ((c0 & c1) != 0) return null;
                double x, y;
                var c = c0 == 0 ? c1 : c0;
                if ((c & 0b1000) != 0)
                {
                    x = x0 + (x1 - x0) * (this.ymax - y0) / (y1 - y0);
                    y = this.ymax;
                }
                else if ((c & 0b0100) != 0)
                {
                    x = x0 + (x1 - x0) * (this.ymin - y0) / (y1 - y0);
                    y = this.ymin;
                }
                else if ((c & 0b0010) != 0)
                {
                    y = y0 + (y1 - y0) * (this.xmax - x0) / (x1 - x0);
                    x = this.xmax;
                }
                else
                {
                    y = y0 + (y1 - y0) * (this.xmin - x0) / (x1 - x0);
                    x = this.xmin;
                }

                if (c0 != 0)
                {
                    x0 = x;
                    y0 = y;
                    c0 = this._regioncode(x0, y0);
                }
                else
                {
                    x1 = x;
                    y1 = y;
                    c1 = this._regioncode(x1, y1);
                }
            }
        }

        private int _edge(int i, int e0, int e1, List<double> P, int j)
        {
            while (e0 != e1)
            {
                double x = 0, y = 0;
                switch (e0)
                {
                    case 0b0101: 
                        e0 = 0b0100; 
                        continue; // top-left
                    case 0b0100:
                        e0 = 0b0110;
                        x = this.xmax;
                        y = this.ymin; 
                        break; // top
                    case 0b0110: 
                        e0 = 0b0010; 
                        continue; // top-right
                    case 0b0010: e0 = 0b1010;
                        x = this.xmax;
                        y = this.ymax; 
                        break; // right
                    case 0b1010: 
                        e0 = 0b1000; 
                        continue; // bottom-right
                    case 0b1000: 
                        e0 = 0b1001;
                        x = this.xmin;
                        y = this.ymax; 
                        break; // bottom
                    case 0b1001: 
                        e0 = 0b0001; 
                        continue; // bottom-left
                    case 0b0001:
                        e0 = 0b0101;
                        x = this.xmin;
                        y = this.ymin; 
                        break; // left
                }
                if (j <= P.Count() && (j == P.Count() || P[j] != x || P[j + 1] != y) && this.contains(i, x, y))
                {
                    P.Insert(j, x);
                    P.Insert(j + 1, y);
                    j += 2;
                }
            }
            return j;
        }

        public int _edgecode(double x, double y)
        {
            return (x == this.xmin ? 0b0001
                : x == this.xmax ? 0b0010 : 0b0000)
                | (y == this.ymin ? 0b0100
                : y == this.ymax ? 0b1000 : 0b0000);
        }

        private double[] _project(double x0, double y0, double vx, double vy)
        {
            var t = double.PositiveInfinity;
            double c, x = 0, y = 0;
            if (vy < 0)
            { // top
                if (y0 <= this.ymin) return null;
                if ((c = (this.ymin - y0) / vy) < t)
                {
                    y = this.ymin;
                    x = x0 + (t = c) * vx;
                }
            }
            else if (vy > 0)
            { // bottom
                if (y0 >= this.ymax) return null;
                if ((c = (this.ymax - y0) / vy) < t)
                {
                    y = this.ymax;
                    x = x0 + (t = c) * vx;
                }
            }
            if (vx > 0)
            { // right
                if (x0 >= this.xmax) return null;
                if ((c = (this.xmax - x0) / vx) < t)
                {
                    x = this.xmax;
                    y = y0 + (t = c) * vy;
                }
            }
            else if (vx < 0)
            { // left
                if (x0 <= this.xmin) return null;
                if ((c = (this.xmin - x0) / vx) < t)
                {
                    x = this.xmin;
                    y = y0 + (t = c) * vy;
                }
            }
            return new double[] { x, y };
        }
        private int _regioncode(double x, double y)
        {
            return (x < this.xmin ? 0b0001
                : x > this.xmax ? 0b0010 : 0b0000)
                | (y < this.ymin ? 0b0100
                : y > this.ymax ? 0b1000 : 0b0000);
        }


        public class Bounds
        {
            public double x0 { get; set; }
            public double y0 { get; set; }
            public double x1 { get; set; }
            public double y1 { get; set; }
        }

    }
}
