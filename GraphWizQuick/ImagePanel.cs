using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace GraphWizQuick
{
    // Hacked from http://www.codeproject.com/Articles/21097/PictureBox-Zoom
    public partial class 
        ImagePanel : UserControl
    {
        public ImagePanel()
        {
            InitializeComponent();

            // Set the value of the double-buffering style bits to true.
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
              ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
              ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        }
        public delegate void myDataChangedDelegate(object sender, float newZoom);
        public event myDataChangedDelegate ZoomChangedEvent;
        public virtual void OnZoomChange(float newZoom)
        {
            if (ZoomChangedEvent != null) ZoomChangedEvent(this, newZoom);
        }

        int viewRectWidth, viewRectHeight; // view window width and height

        float zoom = 1.0f;
        int zoomPercent = 100;
        void ChangeBar(ScrollBar bar, int delta) {
            var minimum = 0;
            var maximum = bar.Maximum - bar.LargeChange;
            if (maximum <= 0) return;  // happens when the entire area is visible
            var value = bar.Value - (int)(delta * bar.LargeChange / (120.0 * 12.0 / SystemInformation.MouseWheelScrollLines));
            bar.Value = Math.Min(Math.Max(value, minimum), maximum);
            return;
        }
        // [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        [DllImport("User32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        // wish there was a better way of doing this but just using OnMouseWheel dosn't cover if the mouse
        // is over the other two scroll bars, and you have to write alot more code to cover those sineros when
        // we can just intercept the messages in a single simple function
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            const long MK_CONTROL = 0x0008;
            const long MK_SHIFT = 0x0004;
            // make sure it refers to this control or the scroll bars
            if (m.Msg == WM_MOUSEWHEEL && ((m.HWnd == this.Handle || m.HWnd == this.hScrollBar1.Handle || m.HWnd == this.vScrollBar1.Handle)))
            {
                long wparm = (long)m.WParam; // use longs just in case we ever run this as 64 bits
                long filtered = (wparm&0xFFFF) & ~(MK_CONTROL & MK_SHIFT);
                if (filtered == MK_CONTROL)
                {
                    short delta = (short)(wparm >> 16);
                    int newZoomPercent = ZoomPercent + (delta > 0 ? 10 : -10);
                    if (newZoomPercent < 10) return; // nothing less than 10%
                    ZoomPercent = newZoomPercent;
                } else if(filtered == MK_SHIFT)
                {
                    if (this.hScrollBar1.Visible)
                    {
                        PostMessage(this.hScrollBar1.Handle, m.Msg, new IntPtr(~filtered & wparm), m.LParam); // we don't 
                    }
                } else
                {
                    if (this.vScrollBar1.Visible)
                    {
                        PostMessage(this.vScrollBar1.Handle, m.Msg, new IntPtr(~filtered & wparm), m.LParam);
                    }
                }
            } else base.WndProc(ref m);
        }
#if false
        // old code that messed with the mouse wheel
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                int oldZoomPercent = (int)(Math.Floor(Zoom / 1.0f * 100.0f));
                int newZoomPercent = oldZoomPercent + (e.Delta > 0 ? 10 : -10);
                if (newZoomPercent < 10) return; // nothing less than 10%
                Zoom = (float)newZoomPercent / 100.0f;
                this.Invalidate();
            }
            else if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                //  this.hScrollBar1.Focus();
                ChangeBar(this.hScrollBar1, e.Delta);
                this.Invalidate();
            }
            else
            {
                //   this.vScrollBar1.Focus();
                ChangeBar(this.vScrollBar1, e.Delta);
                this.Invalidate();
            }
         //   base.OnMouseWheel(e);
        }
#endif
       
        public int ZoomPercent // I made this cause we kept getting rounding errors doing simple 10% incrments
        {
            get { return zoomPercent; }
            set
            {
                if (zoomPercent == value || value < 10) return;
                zoomPercent = value;
                zoom = (float)zoomPercent / 100.0f;
                displayScrollbar();
                setScrollbarValues();
                Invalidate();
                OnZoomChange(zoom);
            }
        }
        public float Zoom
        {
            get { return zoom; }
            set
            {
                // don't use round, we start to get rounding errors at that point
                if (value < 0.10) return; // can't be less than zero, makes no sence as well as less than 10%
                float difference = Math.Abs(value * 0.01f);
                if (Math.Abs(value - zoom) <= difference) return; // too close, not enough change, needs atleast a % point
                zoomPercent = (int)(Math.Floor(value / 1.0f * 100.0f)); // we make sure we trim the last 3 digits;
                zoom = value;
                displayScrollbar();
                setScrollbarValues();
                Invalidate();
                OnZoomChange(value);
            }
        }

        Size canvasSize = new Size(60, 40);
        public Size CanvasSize
        {
            get { return canvasSize; }
            set
            {
                if (canvasSize == value) return;
                canvasSize = value;
                displayScrollbar();
                setScrollbarValues();
                Invalidate();
            }
        }

        Bitmap image;
        private VScrollBar vScrollBar1;
        private HScrollBar hScrollBar1;

        public Bitmap Image
        {
            get { return image; }
            set 
            {
                if (image == value) return;
                if (value == null) ZoomPercent = 100;// reset zoom
                image = value;
                displayScrollbar();
                setScrollbarValues(); 
                Invalidate();
            }
        }

        InterpolationMode interMode = InterpolationMode.NearestNeighbor;
        public InterpolationMode InterpolationMode
        {
            get{return interMode;}
            set{interMode=value;}
        }

        protected override void OnLoad(EventArgs e)
        {
            displayScrollbar();
            setScrollbarValues();
            base.OnLoad(e);
        }

        protected override void OnResize(EventArgs e)
        {
            displayScrollbar();
            setScrollbarValues();
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
             base.OnPaint(e);

            //draw image
            if(image!=null)
            {
                Rectangle srcRect,distRect;
                Point pt=new Point((int)(hScrollBar1.Value/zoom),(int)(vScrollBar1.Value/zoom));
                if (canvasSize.Width * zoom < viewRectWidth && canvasSize.Height * zoom < viewRectHeight)
                    srcRect = new Rectangle(0, 0, canvasSize.Width, canvasSize.Height);  // view all image
                else srcRect = new Rectangle(pt, new Size((int)(viewRectWidth / zoom), (int)(viewRectHeight / zoom))); // view a portion of image

                distRect=new Rectangle((int)(-srcRect.Width/2),-srcRect.Height/2,srcRect.Width,srcRect.Height); // the center of apparent image is on origin
 
                Matrix mx=new Matrix(); // create an identity matrix
                mx.Scale(zoom,zoom); // zoom image
                mx.Translate(viewRectWidth/2.0f,viewRectHeight/2.0f, MatrixOrder.Append); // move image to view window center

                Graphics g=e.Graphics;
                g.InterpolationMode=interMode;
                g.PixelOffsetMode = PixelOffsetMode.Half; //Added
                g.Transform=mx;
                g.DrawImage(image,distRect,srcRect, GraphicsUnit.Pixel);
            }

        }

        private void displayScrollbar()
        {
            viewRectWidth = this.Width;
            viewRectHeight = this.Height;
            
            if (image != null) canvasSize = image.Size;

            // If the zoomed image is wider than view window, show the HScrollBar and adjust the view window
            if (viewRectWidth > canvasSize.Width*zoom)
            {
                hScrollBar1.Visible = false;
                viewRectHeight = Height;
            }
            else
            {
                hScrollBar1.Visible = true;
                viewRectHeight = Height - hScrollBar1.Height;
            }

            // If the zoomed image is taller than view window, show the VScrollBar and adjust the view window
            if (viewRectHeight > canvasSize.Height*zoom)
            {
                vScrollBar1.Visible = false;
                viewRectWidth = Width;
            }
            else
            {
                vScrollBar1.Visible = true;
                viewRectWidth = Width - vScrollBar1.Width;
            }

            // Set up scrollbars
            hScrollBar1.Location = new Point(0, Height - hScrollBar1.Height);
            hScrollBar1.Width = viewRectWidth;
            vScrollBar1.Location = new Point(Width - vScrollBar1.Width, 0);
            vScrollBar1.Height = viewRectHeight;
        }

        private void setScrollbarValues()
        {
            // Set the Maximum, Minimum, LargeChange and SmallChange properties.
            this.vScrollBar1.Minimum = 0;
            this.hScrollBar1.Minimum = 0;

            // If the offset does not make the Maximum less than zero, set its value. 
            if ((canvasSize.Width * zoom - viewRectWidth) > 0)
            {
                this.hScrollBar1.Maximum =(int)( canvasSize.Width * zoom) - viewRectWidth;
            }
            // If the VScrollBar is visible, adjust the Maximum of the 
            // HSCrollBar to account for the width of the VScrollBar.  
            if (this.vScrollBar1.Visible)
            {
                this.hScrollBar1.Maximum += this.vScrollBar1.Width;
            }
            this.hScrollBar1.LargeChange = this.hScrollBar1.Maximum / 10;
            this.hScrollBar1.SmallChange = this.hScrollBar1.Maximum / 20;

            // Adjust the Maximum value to make the raw Maximum value 
            // attainable by user interaction.
            this.hScrollBar1.Maximum += this.hScrollBar1.LargeChange;

            // If the offset does not make the Maximum less than zero, set its value.    
            if ((canvasSize.Height * zoom - viewRectHeight) > 0)
            {
                this.vScrollBar1.Maximum = (int)(canvasSize.Height * zoom) - viewRectHeight;
            }

            // If the HScrollBar is visible, adjust the Maximum of the 
            // VSCrollBar to account for the width of the HScrollBar.
            if (this.hScrollBar1.Visible)
            {
                this.vScrollBar1.Maximum += this.hScrollBar1.Height;
            }
            this.vScrollBar1.LargeChange = this.vScrollBar1.Maximum / 10;
            this.vScrollBar1.SmallChange = this.vScrollBar1.Maximum / 20;

            // Adjust the Maximum value to make the raw Maximum value 
            // attainable by user interaction.
            this.vScrollBar1.Maximum += this.vScrollBar1.LargeChange;
        }

        private void InitializeComponent()
        {
            this.vScrollBar1 = new System.Windows.Forms.VScrollBar();
            this.hScrollBar1 = new System.Windows.Forms.HScrollBar();
            this.SuspendLayout();
            // 
            // vScrollBar1
            // 
            this.vScrollBar1.Location = new System.Drawing.Point(95, 21);
            this.vScrollBar1.Name = "vScrollBar1";
            this.vScrollBar1.Size = new System.Drawing.Size(17, 80);
            this.vScrollBar1.TabIndex = 1;
            this.vScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBar1_Scroll);
            // 
            // hScrollBar1
            // 
            this.hScrollBar1.Location = new System.Drawing.Point(32, 119);
            this.hScrollBar1.Name = "hScrollBar1";
            this.hScrollBar1.Size = new System.Drawing.Size(80, 17);
            this.hScrollBar1.TabIndex = 2;
            this.hScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBar1_Scroll);
            // 
            // ImagePanel
            // 
            this.Controls.Add(this.hScrollBar1);
            this.Controls.Add(this.vScrollBar1);
            this.Name = "ImagePanel";
            this.ResumeLayout(false);

        }

        private void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.Invalidate();
        }
    }
}
