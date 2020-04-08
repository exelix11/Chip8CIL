using FastBitmapLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DIsplayApp
{
	public partial class DisplayForm : Form
	{
		public PictureBox pictureBox1;
		public Bitmap image;

		public DisplayForm(Bitmap image)
		{
			InitializeComponent();

			this.FormClosed += closed;

			pictureBox1 = new PictureBoxWithInterpolationMode() { InterpolationMode = InterpolationMode.NearestNeighbor };
			this.Controls.Add(pictureBox1);
			pictureBox1.Dock = DockStyle.Fill;
			pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
			this.image = image;
			pictureBox1.Image = image;
		}

		private void closed(object sender, FormClosedEventArgs e)
		{
			Application.Exit();
		}

		public void InvokeDraw(Memory<byte> VMEM)
		{
			this.Invoke((MethodInvoker) delegate 
			{
				this.Draw(VMEM.Span);
			});
		}

		public void Draw(Span<byte> VMEM)
		{
			using var img = new FastBitmap(image);
			img.Lock();

			Color black = Color.Black;
			Color white = Color.White;

			for (int y = 0; y < image.Height; y++)
			{
				int yOff = y * image.Width / 8;
				byte mask = 0x80; int lineIndex = 0;
				for (int x = 0; x < image.Width; x++)
				{
					if ((VMEM[yOff + lineIndex] & mask) == 0)
						img.SetPixel(x, y, black);
					else
						img.SetPixel(x, y, white);

					mask >>= 1;
					if (mask == 0)
					{
						lineIndex++;
						mask = 0x80;
					}
				}
			}
			img.Unlock();
			this.Refresh();
		}
	}

	public class PictureBoxWithInterpolationMode : PictureBox
	{
		public InterpolationMode InterpolationMode { get; set; }

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			paintEventArgs.Graphics.InterpolationMode = InterpolationMode;
			base.OnPaint(paintEventArgs);
		}
	}
}
