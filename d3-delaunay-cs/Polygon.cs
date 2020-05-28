using System.Collections.Generic;
using System.Linq;

namespace d3_delaunay_cs
{
    public class Polygon : IPathable<List<List<double>>>
    {
        List<List<double>> _;

        public Polygon()
        {
            this._ = new List<List<double>>();
        }

        public void moveTo(double x, double y)
        {
            this._.Add(new List<double> { x, y });
        }
        public void closePath()
        {
            this._.Add(this._[0].ToList());
        }

        public void lineTo(double x, double y)

        {
            this._.Add(new List<double> { x, y });
        }

        public List<List<double>> value()
        {
            return this._.Count() != 0 ? this._ : null;
        }
    }
}
