using System;

namespace d3_delaunay_cs
{
    public class Path : IPathable<string>
    {
        const double epsilon = 1e-6;
        double? _x0, _y0, _x1, _y1;
        string _;

        public Path()
        {
            _x0 = _y0 = _x1 = _y1 = null;
            _ = "";
        }

        public void moveTo(double x, double y)
        {
            this._x0 = this._x1 = x;
            this._y0 = this._y1 = y;
            this._ += $"M{x},{y}";
        }

        public void closePath()
        {
            if (this._x1 != null)
            {
                this._x1 = this._x0;
                this._y1 = this._y0;
                this._ += "Z";
            }
        }

        public void lineTo(double x, double y)
        {
            this._x1 = x;
            this._y1 = y;
            this._ += $"L{x},{y}";
        }

        public void arc(double x, double y, double r)
        {
            var x0 = x + r;
            var y0 = y;
            if (r < 0) throw new Exception("negative radius");
            if (this._x1 == null) this._ += $"M{x0},{y0}";
            else if (
                Math.Abs((this._x1 ?? 0) - x0) > epsilon || 
                Math.Abs((this._y1 ?? 0 - y0)) > epsilon)
                    this._ += $"L{x0},{y0}";
            if (r == 0) return;
            this._x1 = x0;
            this._y1 = y0;
            this._ += $"A{r},{r},0,1,1,{x - r},{y}A{r},{r},0,1,1,{x0},{y0}";
        }
        public void rect(double x, double y, double w, double h)
        {
            this._x0 = this._x1 = x;
            this._y0 = this._y1 = y;
            this._ += $"M{x},{y}h{w}v{h}h{w}Z";
        }
        public string value()
        {
            return this._ == "" ? null : this._;
        }
    }
}
