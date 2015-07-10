using System;
using System.Drawing;
using System.Windows.Forms;

namespace Free.Controls.OpenStreetMap
{
	public partial class PositionPickerDialog : Form
	{
		Bitmap image;

		bool markerSet;
		double markerPositionX, markerPositionY;

		public double MarkerPositionX { get { return markerPositionX; } }
		public double MarkerPositionY { get { return markerPositionY; } }
		public PointD MarkerPosition { get { return new PointD(markerPositionX, markerPositionY); } }

		public PositionPickerDialog()
		{
			image=new Bitmap(GetType(), "location_marker_small.png");

			InitializeComponent();

			DeleteMarker();

			mapPanel.ZoomLevel=0;
			mapPanel.X=0;
			mapPanel.Y=0;
		}

		public PositionPickerDialog(double x, double y)
		{
			image=new Bitmap(GetType(), "location_marker_small.png");

			InitializeComponent();

			SetMarker(x, y);

			mapPanel.ZoomLevel=5;
			mapPanel.X=markerPositionX;
			mapPanel.Y=markerPositionY;
		}

		public BorderStyle MapPanelBorderStyle
		{
			get { return mapPanel.BorderStyle; }
			set { mapPanel.BorderStyle=value; }
		}

		public void SetMarker(double x, double y)
		{
			markerSet=true;
			markerPositionX=x;
			markerPositionY=y;

			buttonOK.Enabled=true;

			textBoxPosition.Text=string.Format("{0} {1}", OpenStreetMapPanel.dtodms(x, 'E', 'W'), OpenStreetMapPanel.dtodms(y, 'N', 'S'));
		}

		public void DeleteMarker()
		{
			markerSet=false;
			markerPositionX=0;
			markerPositionY=0;

			buttonOK.Enabled=false;
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			DialogResult=DialogResult.OK;
			Close();
		}

		private void buttonCancel_Click(object sender, EventArgs e)
		{
			DialogResult=DialogResult.Cancel;
			Close();
		}

		private void mapPanel_MapPainted(object sender, PaintEventArgs e, double xmin, double ymin, double xscale, double yscale)
		{
			if(!markerSet) return;

			PointD nw=OpenStreetMapPanel.GetTileIndex(mapPanel.ZoomLevel, markerPositionX, markerPositionY);
			int x=(int)(xscale*(nw.X-xmin));
			int y=(int)(yscale*(nw.Y-ymin));
			e.Graphics.DrawImage(image, x-image.Width/2, y-image.Height, image.Width, image.Height);
		}

		private void mapPanel_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			SetMarker(mapPanel.MouseXWGS84, mapPanel.MouseYWGS84);

			DialogResult=DialogResult.OK;
			Close();
		}

		private void mapPanel_NonDragingMouseClick(object sender, double x, double y)
		{
			SetMarker(x, y);
		}
	}
}
