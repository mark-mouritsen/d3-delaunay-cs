using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace d3_delaunay_cs
{

    public class Delaunator
    {
        public double EPSILON = Math.Pow(2, -52);

        public double[] coords;

        public double minX = double.PositiveInfinity;
        public double minY = double.PositiveInfinity;
        public double maxX = double.NegativeInfinity;
        public double maxY = double.NegativeInfinity;

        private int n;
        UInt32[] ids;

        private double _cx;
        private double _cy;
        private int _hashSize;
        private Node[] _hash;

        public Node hull;

        public uint[] triangles;
        public int[] halfedges;

        private int trianglesLen;

        static Func<double[], double> defaultGetX = (double[] p) => p[0];
        static Func<double[], double> defaultGetY = (double[] p) => p[1];

        public static Delaunator from(
            double[][] points, 
            Func<double[], double> getX = null, 
            Func<double[], double> getY = null)
        {
            getX = getX ?? defaultGetX;
            getY = getY ?? defaultGetY;

            var n = points.Length;
            var coords = new double[n * 2];

            for (var i = 0; i < n; i++)
            {
                var p = points[i];
                coords[2 * i] = getX(p);
                coords[2 * i + 1] = getY(p);
            }

            return new Delaunator(coords);
        }

        public Delaunator(double[] coords)
        {
            n = coords.Length >> 1;
            ids = new uint[n];

            this.coords = coords;

            for (uint i = 0; i < n; i++)
            {
                var x = coords[2 * i];
                var y = coords[2 * i + 1];
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                ids[i] = i;
            }

            var cx = (minX + maxX) / 2;
            var cy = (minY + maxY) / 2;

            var minDist = double.PositiveInfinity;
            uint i0 = 0, i1 = 0, i2 = 0;

            // pick a seed point close to the centroid
            for (uint i = 0; i < n; i++)
            {
                var d = dist(cx, cy, coords[2 * i], coords[2 * i + 1]);
                if (d < minDist)
                {
                    i0 = i;
                    minDist = d;
                }
            }

            var i0x = coords[2 * i0];
            var i0y = coords[2 * i0 + 1];

            minDist = double.PositiveInfinity;

            // find the point closest to the seed
            for (uint i = 0; i < n; i++)
            {
                if (i == i0) continue;
                var d = dist(i0x, i0y, coords[2 * i], coords[2 * i + 1]);
                if (d < minDist && d > 0)
                {
                    i1 = i;
                    minDist = d;
                }
            }

            var i1x = coords[2 * i1];
            var i1y = coords[2 * i1 + 1];

            var minRadius = double.PositiveInfinity;

            // find the third point which forms the smallest circumcircle with the first two
            for (uint i = 0; i < n; i++)
            {
                if (i == i0 || i == i1) continue;
                var r = circumradius(i0x, i0y, i1x, i1y, coords[2 * i], coords[2 * i + 1]);
                if (r < minRadius)
                {
                    i2 = i;
                    minRadius = r;
                }
            }
            var i2x = coords[2 * i2];
            var i2y = coords[2 * i2 + 1];

            if (minRadius == double.PositiveInfinity)
            {
                throw new Exception("No Delaunay triangulation exists for this input.");
            }

            // swap the order of the seed points for counter-clockwise orientation
            if (orient(i0x, i0y, i1x, i1y, i2x, i2y))
            {
                var i = i1;
                var x = i1x;
                var y = i1y;
                i1 = i2;
                i1x = i2x;
                i1y = i2y;
                i2 = i;
                i2x = x;
                i2y = y;
            }

            var center = circumcenter(i0x, i0y, i1x, i1y, i2x, i2y);
            this._cx = center.x;
            this._cy = center.y;

            // sort the points by distance from the seed triangle circumcenter
            quicksort(ids, coords, 0, ids.Length - 1, center.x, center.y);

            // initialize a hash table for storing edges of the advancing convex hull
            this._hashSize = (int)Math.Ceiling(Math.Sqrt(n));
            this._hash = new Node[this._hashSize];

            // initialize a circular doubly-linked list that will hold an advancing convex hull

            var e = this.hull = insertNode(coords, i0);
            this._hashEdge(e);
            e.t = 0;
            e = insertNode(coords, i1, e);
            this._hashEdge(e);
            e.t = 1;
            e = insertNode(coords, i2, e);
            this._hashEdge(e);
            e.t = 2;

            var maxTriangles = 2 * n - 5;
            var triangles = this.triangles = new uint[maxTriangles * 3];
            var halfedges = this.halfedges = new int[maxTriangles * 3];

            this.trianglesLen = 0;

            this._addTriangle(i0, i1, i2, -1, -1, -1);

            double xp = 0, yp = 0;
            for (var k = 0; k < ids.Length; k++)
            {
                var i = ids[k];
                var x = coords[2 * i];
                var y = coords[2 * i + 1];

                // skip near-duplicate points
                if (k > 0 && Math.Abs(x - xp) <= EPSILON && Math.Abs(y - yp) <= EPSILON) continue;
                xp = x;
                yp = y;

                // skip seed triangle points
                if (i == i0 || i == i1 || i == i2) continue;

                // find a visible edge on the convex hull using edge hash
                var startKey = this._hashKey(x, y);
                var key = startKey;
                Node start;
                do
                {
                    start = this._hash[key];
                    key = (key + 1) % this._hashSize;
                } while ((start == null || start.removed) && key != startKey);

                start = start.prev;
                e = start;
                while (!orient(x, y, e.x, e.y, e.next.x, e.next.y))
                {
                    e = e.next;
                    if (e == start)
                    {
                        e = null;
                        break;
                    }
                }
                // likely a near-duplicate point; skip it
                if (e == null) continue;

                var walkBack = e == start;

                // add the first triangle from the point
                var t = this._addTriangle(e.i, i, e.next.i, -1, -1, e.t);

                e.t = t; // keep track of boundary triangles on the hull
                e = insertNode(coords, i, e);

                // recursively flip triangles from the point until they satisfy the Delaunay condition
                e.t = this._legalize(t + 2);

                // walk forward through the hull, adding more triangles and flipping recursively
                var q = e.next;
                while (orient(x, y, q.x, q.y, q.next.x, q.next.y))
                {
                    t = this._addTriangle(q.i, i, q.next.i, q.prev.t, -1, q.t);
                    q.prev.t = this._legalize(t + 2);
                    this.hull = removeNode(q);
                    q = q.next;
                }

                if (walkBack)
                {
                    // walk backward from the other side, adding more triangles and flipping
                    q = e.prev;
                    while (orient(x, y, q.prev.x, q.prev.y, q.x, q.y))
                    {
                        t = this._addTriangle(q.prev.i, i, q.i, -1, q.t, q.prev.t);
                        this._legalize(t + 2);
                        q.prev.t = t;
                        this.hull = removeNode(q);
                        q = q.prev;
                    }
                }

                // save the two new edges in the hash table
                this._hashEdge(e);
                this._hashEdge(e.prev);
            }

            // trim typed triangle mesh arrays
            var tempTriangles = new uint[this.trianglesLen];
            var tempHalfedges = new int[this.trianglesLen];
            Array.Copy(this.triangles, tempTriangles, this.trianglesLen);
            Array.Copy(this.halfedges, tempHalfedges, this.trianglesLen);
            this.triangles = tempTriangles;
            this.halfedges = tempHalfedges;
        }

        private void _hashEdge(Node e)
        {
            this._hash[this._hashKey(e.x, e.y)] = e;
        }

        private int _hashKey(double x, double y)
        {
            return (int)(Math.Floor(pseudoAngle(x - this._cx, y - this._cy) * this._hashSize) % this._hashSize);
        }

        private int _legalize(int a)
        {
            var triangles = this.triangles;
            var coords = this.coords;
            var halfedges = this.halfedges;

            var b = halfedges[a];

            /* if the pair of triangles doesn't satisfy the Delaunay condition
             * (p1 is inside the circumcircle of [p0, pl, pr]), flip them,
             * then do the same check/flip recursively for the new pair of triangles
             *
             *           pl                    pl
             *          /||\                  /  \
             *       al/ || \bl            al/    \a
             *        /  ||  \              /      \
             *       /  a||b  \    flip    /___ar___\
             *     p0\   ||   /p1   =>   p0\---bl---/p1
             *        \  ||  /              \      /
             *       ar\ || /br             b\    /br
             *          \||/                  \  /
             *           pr                    pr
             */
            var a0 = a - a % 3;
            var b0 = b - b % 3;

            var al = a0 + (a + 1) % 3;
            var ar = a0 + (a + 2) % 3;
            var bl = b0 + (b + 2) % 3;

            if (b == -1) return ar;

            var p0 = triangles[ar];
            var pr = triangles[a];
            var pl = triangles[al];
            var p1 = triangles[bl];

            var illegal = inCircle(
                coords[2 * p0], coords[2 * p0 + 1],
                coords[2 * pr], coords[2 * pr + 1],
                coords[2 * pl], coords[2 * pl + 1],
                coords[2 * p1], coords[2 * p1 + 1]);

            if (illegal)
            {
                triangles[a] = p1;
                triangles[b] = p0;

                var hbl = halfedges[bl];

                // edge swapped on the other side of the hull (rare); fix the halfedge reference
                if (hbl == -1)
                {
                    var e = this.hull;
                    do
                    {
                        if (e.t == bl)
                        {
                            e.t = a;
                            break;
                        }
                        e = e.next;
                    } while (e != this.hull);
                }
                this._link(a, hbl);
                this._link(b, halfedges[ar]);
                this._link(ar, bl);

                var br = b0 + (b + 1) % 3;

                this._legalize(a);
                return this._legalize(br);
            }

            return ar;
        }


        private void _link(int a, int b)
        {
            this.halfedges[a] = b;
            if (b != -1) this.halfedges[b] = a;
        }

        // add a new triangle given vertex indices and adjacent half-edge ids
        private int _addTriangle(uint i0, uint i1, uint i2, int a, int b, int c)
        {
            var t = this.trianglesLen;

            this.triangles[t] = i0;
            this.triangles[t + 1] = i1;
            this.triangles[t + 2] = i2;

            this._link(t, a);
            this._link(t + 1, b);
            this._link(t + 2, c);

            this.trianglesLen += 3;

            return t;
        }

        // create a new node in a doubly linked list
        private Node insertNode(double[] coords, uint i, Node prev = null)
        {
            var node = new Node
            {
                i = i,
                x = coords[2 * i],
                y = coords[2 * i + 1],
                t = 0,
                prev = null,
                next = null,
                removed = false
            };

            if (prev == null)
            {
                node.prev = node;
                node.next = node;
            }
            else
            {
                node.next = prev.next;
                node.prev = prev;
                prev.next.prev = node;
                prev.next = node;
            }
            return node;
        }

        private Node removeNode(Node node)
        {
            node.prev.next = node.next;
            node.next.prev = node.prev;
            node.removed = true;
            return node.prev;
        }

        private double pseudoAngle(double dx, double dy)
        {
            var p = dx / (Math.Abs(dx) + Math.Abs(dy));
            return (dy > 0 ? 3 - p : 1 + p) / 4; // [0..1]
        }


        private double dist(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        private bool inCircle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
        {
            var dx = ax - px;
            var dy = ay - py;
            var ex = bx - px;
            var ey = by - py;
            var fx = cx - px;
            var fy = cy - py;

            var ap = dx * dx + dy * dy;
            var bp = ex * ex + ey * ey;
            var cp = fx * fx + fy * fy;

            return dx * (ey * cp - bp * fy) -
                   dy * (ex * cp - bp * fx) +
                   ap * (ex * fy - ey * fx) < 0;
        }

        private double circumradius(double ax, double ay, double bx, double by, double cx, double cy)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var ex = cx - ax;
            var ey = cy - ay;

            var bl = dx * dx + dy * dy;
            var cl = ex * ex + ey * ey;
            var d = dx * ey - dy * ex;

            var x = (ey * bl - dy * cl) * 0.5f / d;
            var y = (dx * cl - ex * bl) * 0.5f / d;

            // TODO optimize
            return (bl != 0 && cl != 0 && d != 0 && (x * x + y * y) != 0) 
                ? (x * x + y * y) 
                : double.PositiveInfinity;
        }

        private Point circumcenter(double ax, double ay, double bx, double by, double cx, double cy)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var ex = cx - ax;
            var ey = cy - ay;

            var bl = dx * dx + dy * dy;
            var cl = ex * ex + ey * ey;
            var d = dx * ey - dy * ex;

            var x = ax + (ey * bl - dy * cl) * 0.5f / d;
            var y = ay + (dx * cl - ex * bl) * 0.5f / d;

            return new Point { x = x, y = y };
        }

        private bool orient(double px, double py, double qx, double qy, double rx, double ry)
        {
            return (qy - py) * (rx - qx) - (qx - px) * (ry - qy) < 0;
        }

        private void quicksort(uint[] ids, double[] coords, int left, int right, double cx, double cy)
        {
            int i, j;
            uint temp;

            if (right - left <= 20)
            {
                for (i = left + 1; i <= right; i++)
                {
                    temp = ids[i];
                    j = i - 1;
                    while (j >= left && compare(coords, ids[j], temp, cx, cy) > 0) ids[j + 1] = ids[j--];
                    ids[j + 1] = temp;
                }
            }
            else
            {
                var median = (left + right) >> 1;
                i = left + 1;
                j = right;
                swap(ids, median, i);
                if (compare(coords, ids[left], ids[right], cx, cy) > 0) swap(ids, left, right);
                if (compare(coords, ids[i], ids[right], cx, cy) > 0) swap(ids, i, right);
                if (compare(coords, ids[left], ids[i], cx, cy) > 0) swap(ids, left, i);

                temp = ids[i];
                while (true)
                {
                    do i++; while (compare(coords, ids[i], temp, cx, cy) < 0);
                    do j--; while (compare(coords, ids[j], temp, cx, cy) > 0);
                    if (j < i) break;
                    swap(ids, i, j);
                }
                ids[left + 1] = ids[j];
                ids[j] = temp;

                if (right - i + 1 >= j - left)
                {
                    quicksort(ids, coords, i, right, cx, cy);
                    quicksort(ids, coords, left, j - 1, cx, cy);
                }
                else
                {
                    quicksort(ids, coords, left, j - 1, cx, cy);
                    quicksort(ids, coords, i, right, cx, cy);
                }
            }
        }

        private double compare(double[] coords, uint i, uint j, double cx, double cy)
        {
            var d1 = dist(coords[2 * i], coords[2 * i + 1], cx, cy);
            var d2 = dist(coords[2 * j], coords[2 * j + 1], cx, cy);
            var d1md2 = d1 - d2;
            if (d1md2 != 0) return d1md2;
            var cimcj = coords[2 * i] - coords[2 * j];
            if (cimcj != 0) return cimcj;
            return coords[2 * i + 1] - coords[2 * j + 1];
        }

        private void swap(uint[] arr, int i, int j)
        {
            var tmp = arr[i];
            arr[i] = arr[j];
            arr[j] = tmp;
        }

        private class Point
        {
            public double x { get; set; }
            public double y { get; set; }
        }

        public class Node
        {
            public uint i { get; set; }

            public double x { get; set; }
            public double y { get; set; }
            public int t { get; set; }
            public Node prev { get; set; }
            public Node next { get; set; }
            public bool removed { get; set; }
        }
    }
}
