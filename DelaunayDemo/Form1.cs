using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

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

        DelaunayVoronoi d1;

        int hoverIndex = -1;
        bool recalculating = false;

        public Form1()
        {
            InitializeComponent();
            EngineInit();
        }

        private void EngineInit()
        {
            //InitNoise(seed);
            d1 = new DelaunayVoronoi(CANVAS_WIDTH, CANVAS_HEIGHT, (int)seedInput.Value, (int)radiusInput.Value);

            renderThread = new Thread(new ThreadStart(render));
            renderThread.Start();
        }

        private unsafe void render()
        {
            int framesRendered = 0;
            long startTime = Environment.TickCount;

            Bitmap frame = new Bitmap(CANVAS_WIDTH, CANVAS_HEIGHT);
            Graphics frameGraphics = Graphics.FromImage(frame);

            while (true)
            {
                if (recalculating) { continue;  }
                if (drawHandle == null) { continue; }
                //debug
                //object obj = form.Invoke(new Action(() => { form.PointToClient(Cursor.Position); }));
                //frameGraphics.DrawString(Convert.ToString(Cursor.Position.X), new Font("Arial", 12), new SolidBrush(Color.Black), 0, 0);

                // base
                frameGraphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

                foreach (var polygon in d1.CellPolygons)
                {
                    frameGraphics.DrawPolygon(new Pen(Color.Blue), polygon.Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                }

                foreach (var point in d1.Points)
                {
                    frameGraphics.FillRectangle(new SolidBrush(Color.Blue), point.X, point.Y, 4, 4);
                }

                if (hoverIndex >= 0)
                {
                    var polygon = d1.CellPolygons[hoverIndex];
                    if (polygon.Any(point => double.IsNaN(point[0]) || double.IsNaN(point[1]))) break;
                    frameGraphics.FillPolygon(new SolidBrush(Color.Green), polygon.Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
                    foreach (var polygonIndex in d1.Delaunay.neighbors(hoverIndex))
                    {
                        frameGraphics.FillPolygon(new SolidBrush(Color.Blue), d1.CellPolygons[(int)polygonIndex].Select(point => new Point { X = (int)point[0], Y = (int)point[1] }).ToArray());
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
            hoverIndex = d1.Delaunay.find(e.X, e.Y);
        }

        private void seedInput_ValueChanged(object sender, EventArgs e)
        {
            hoverIndex = -1;
            d1 = new DelaunayVoronoi(CANVAS_WIDTH, CANVAS_HEIGHT, (int)seedInput.Value, (int)radiusInput.Value);
        }

        private void radiusInput_ValueChanged(object sender, EventArgs e)
        {
            hoverIndex = -1;
            d1 = new DelaunayVoronoi(CANVAS_WIDTH, CANVAS_HEIGHT, (int)seedInput.Value, (int)radiusInput.Value);
        }
    }
}
