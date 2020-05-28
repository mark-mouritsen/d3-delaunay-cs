using d3_delaunay_cs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static d3_delaunay_cs.Voronoi;

namespace DelaunayDemo
{
    public partial class Form1 : Form
    {
        //Constants
        public const int CANVAS_WIDTH = 1200;
        public const int CANVAS_HEIGHT = 700;

        //Members
        private Graphics drawHandle;
        private Thread renderThread;

        List<Vector2> points;
        Delaunay delaunay;
        Voronoi voronoi;

        int hoverIndex = -1;

        public Form1()
        {
            InitializeComponent();
            EngineInit();
        }

        private void EngineInit()
        {
            //InitNoise(seed);
            points = UniformPoissonDiskSampler.SampleRectangle(new Vector2(0, 0), new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT), 25);
            var pointsString = string.Join(",", points.Select(point => $"[{point.X},{point.Y}]"));

            delaunay = Delaunay.from(points.Select(point => new double[] { point.X, point.Y }).ToArray());
            voronoi = delaunay.voronoi(new Bounds { x0 = 0.5, y0 = 0.5, x1 = CANVAS_WIDTH - 0.5, y1 = CANVAS_HEIGHT - 0.5 });

            renderThread = new Thread(new ThreadStart(render));
            renderThread.Start();
        }

        private unsafe void render()
        {
            int framesRendered = 0;
            long startTime = Environment.TickCount;

            Bitmap frame = new Bitmap(CANVAS_WIDTH, CANVAS_HEIGHT);
            Graphics frameGraphics = Graphics.FromImage(frame);

            var cellPolygons = voronoi.cellPolygons().ToList();
            while (true)
            {
                if (drawHandle == null) { continue; }
                //debug
                //object obj = form.Invoke(new Action(() => { form.PointToClient(Cursor.Position); }));
                //frameGraphics.DrawString(Convert.ToString(Cursor.Position.X), new Font("Arial", 12), new SolidBrush(Color.Black), 0, 0);

                // base
                frameGraphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

                foreach (var polygon in cellPolygons)
                {
                    frameGraphics.DrawPolygon(new Pen(Color.Blue), polygon.Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                }

                foreach (var point in points)
                {
                    frameGraphics.FillRectangle(new SolidBrush(Color.Blue), point.X, point.Y, 4, 4);
                }

                foreach (var polygonIndex in delaunay.neighbors(80))
                {
                    frameGraphics.FillPolygon(new SolidBrush(Color.Blue), cellPolygons[(int)polygonIndex].Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                }

                if (hoverIndex >= 0)
                {
                    var polygon = cellPolygons[hoverIndex];
                    if (polygon.Any(point => double.IsNaN(point[0]) || double.IsNaN(point[1]))) break;
                    frameGraphics.FillPolygon(new SolidBrush(Color.Green), polygon.Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                    foreach (var polygonIndex in delaunay.neighbors(hoverIndex))
                    {
                        frameGraphics.FillPolygon(new SolidBrush(Color.Blue), cellPolygons[(int)polygonIndex].Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                    }
                }

                //var data = frame.LockBits(new Rectangle(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                //int PixelSize = 4;
                //for (int y = 0; y < data.Height; y++)
                //{
                //    byte* row = (byte*)data.Scan0 + (y * data.Stride);

                //    for (int x = 0; x < data.Width; x++)
                //    {
                //        row[x * PixelSize] = Convert.ToByte(Math.Abs((int)(255)));
                //    }
                //}

                //frame.UnlockBits(data);

                drawHandle.DrawImage(frame, 0, 0);
                /* end draw */

                //*//
                //Benchmarking
                framesRendered++;
                if (Environment.TickCount >= startTime + 1000)
                {
                    Console.WriteLine("GEngine: " + framesRendered + " fps");
                    //Console.WriteLine("Cursor: " + form.Invoke(new Action(() => { form.PointToClient(Cursor.Position);})).ToString());
                    framesRendered = 0;
                    startTime = Environment.TickCount;
                }//*/
            }
        }

        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            drawHandle = canvas.CreateGraphics();
        }

        private void GameWin_Load(object sender, EventArgs e)
        {
            AllocConsole();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            hoverIndex = delaunay.find(e.X, e.Y);
        }
    }
}
