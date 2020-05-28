namespace d3_delaunay_cs
{
    public interface IPathable<T>
    {
        public void moveTo(double x, double y);
        public void closePath();
        public void lineTo(double x, double y);
        public T value();
    }
}
