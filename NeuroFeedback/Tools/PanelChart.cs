using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeuroFeedback
{
    class PanelChart
    {

        private Panel panel;
        private Graphics graphics;
        private Pen back_pen;
        private Pen pen;

        private int T = 0;
        private double val = 0;

        public double y_scale = 0.07;

        public PanelChart(Panel a_panel)
        {
            panel = a_panel;
            panel.Paint += OnPaint;
            panel.Resize += OnResize;
            // pen = new Pen(Color.Gray);
            pen = new Pen(Color.Black);
            back_pen = new Pen(Color.FromArgb(255,255,210));
        }

        private int ToX(double T)
        {
            return (int)(T / 2);
        }

        private int ToY(double val)
        {
            // var y = (int)((panel.Height/2.0) - val % panel.Height * y_scale);
            // return (int)((panel.Height / 2.0) - val * y_scale);
            return (int)((panel.Height - 30) - val * y_scale);
        }
        private Point ToScr(double T, double val)
        {
            return new Point(ToX(T), ToY(val));
        }

        private void Clear()
        {
			Console.WriteLine("clear");
			var b = new SolidBrush(Color.FromArgb(255, 255, 210));
            graphics.FillRectangle(b, 0, 0, panel.Width, panel.Height);
//            graphics.DrawRectangle(new Pen(Color.Gray), 0, 0, panel.Width-1, panel.Height-1);
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
			Console.WriteLine("onPaint");
			Clear();
        }

        private void OnResize(object sender, EventArgs e)
        {
//            throw new NotImplementedException();
        }

        public void DrawNext(double new_val)
        {
            //graphics.DrawLine(back_pen, ToX(T+18), 0, ToX(T+18), panel.Height);
            //graphics.DrawLine(pen, ToScr(T-1, val), ToScr(T,new_val));
            //val = new_val;
            //T++;

            panel.BeginInvoke((Action)(() =>
            {
                graphics = panel.CreateGraphics();
                graphics.DrawLine(pen, ToScr(T - 1, val), ToScr(T, new_val));
                val = new_val;
                T++;

                //Console.WriteLine(panel.Width);
                if (ToX(T) > panel.Width)
                    NewCycle();
            }));
        }

        public void NewCycle()
        {
            T = 0;

            Bitmap B = new Bitmap(panel.Width, panel.Height);
            var BG = Graphics.FromImage(B);

            BG.CopyFromScreen(panel.PointToScreen(new Point(0, 0)), new Point(0, 0), B.Size);
            Clear();
            //panel.DrawToBitmap(B, new Rectangle(0, 0, panel.Width, panel.Height));

            graphics.DrawImageUnscaled(B, 0, -30);

        }

    }
}
