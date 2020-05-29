using d3_delaunay_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static d3_delaunay_cs.Voronoi;

namespace DelaunayDemo
{
    class DelaunayVoronoi
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public int Seed { get; private set; }
        public int Radius { get; private set; }

        public List<Vector2> Points { get; private set; }
        public Delaunay Delaunay { get; private set; }
        public Voronoi Voronoi { get; private set; }
        public List<List<List<double>>> CellPolygons { get; private set; }

        public DelaunayVoronoi(int width, int height, int seed = 1, int radius = 25)
        {
            if (radius <= 0) radius = 25;
            if (width <= 0) width = 500;
            if (height <= 0) height = 500;

            Width = width;
            Height = height;
            Seed = seed;
            Radius = radius;

            Initialize();
        }

        public void Initialize()
        {
            UniformPoissonDiskSampler.Random = new Random(Seed);
            Points = UniformPoissonDiskSampler.SampleRectangle(new Vector2(0, 0), new Vector2(Width, Height), Radius);

            Delaunay = Delaunay.from(Points.Select(point => new double[] { point.X, point.Y }).ToArray());
            Voronoi = Delaunay.voronoi(new Bounds { x0 = 0.5, y0 = 0.5, x1 = Width - 0.5, y1 = Height - 0.5 });

            CellPolygons = Voronoi.cellPolygons().ToList();
        }
    }
}
