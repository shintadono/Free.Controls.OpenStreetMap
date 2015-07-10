using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Free.Controls.OpenStreetMap
{
	public delegate void MapPaintedEventHandler(object sender, PaintEventArgs e, double xmin, double ymin, double xscale, double yscale);
	public delegate void SelectionChangedHandler(object sender, BoundingBoxWGS84 selection);
	public delegate void PositionChangedHandler(object sender, double x, double y);
	public delegate void ZoomChangedHandler(object sender, int zoom);
	public delegate void ClickHandler(object sender, double x, double y);

	public enum SelectionModes
	{
		None,
		Vanish,
		Stay,
		Editable
	}

	public partial class OpenStreetMapPanel : UserControl
	{
		#region Constants&Statics
		static CultureInfo nc=new CultureInfo("");
		public static readonly Size TileSize=new Size(256, 256);
		public const int MinimumZoomLevelOfControl=0;
		public const int MaximumZoomLevelOfControl=18;
		public const double MaxYCoordinate=85.0511287798066;
		#endregion

		#region Coordinate Transformations
		// Returns the tile coordinates for world coordinates
		// Thanks to http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#C.23
		public static PointD GetTileIndex(int zoom, double x1, double y1)
		{
			double x=(x1+180.0)/360.0*(1<<zoom);
			double y=((1.0-Math.Log(Math.Tan(y1*Math.PI/180.0)+1.0/Math.Cos(y1*Math.PI/180.0))/Math.PI)/2.0*(1<<zoom));
			return new PointD(x, y);
		}

		// Returns the world coordinates for tile coordinates
		// Thanks to http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#C.23
		public static PointD GetCoordinates(int zoom, double x1, double y1)
		{
			double n=Math.PI-((2.0*Math.PI*y1)/Math.Pow(2.0, zoom));
			double x=(x1/Math.Pow(2.0, zoom)*360.0)-180.0;
			double y=180.0/Math.PI*Math.Atan(0.5*(Math.Exp(n)-Math.Exp(-n)));
			return new PointD(x, y);
		}

		// Calculates the maximum zoom level, with which both coordinates still fit into the given size
		public static int GetOptimalZoomLevel(Size size, double west, double north, double east, double south)
		{
			return GetOptimalZoomLevel(size, west, north, east, south, MinimumZoomLevelOfControl, MaximumZoomLevelOfControl);
		}

		public static int GetOptimalZoomLevel(Size size, double west, double north, double east, double south, int minZoom, int maxZoom)
		{
			double xTiles=(double)size.Width/TileSize.Width;
			double xDistance=west-east;

			double yTiles=(double)size.Height/TileSize.Height;
			double yDistance=north-south;

			double distancePerTile=Math.Max(xDistance/xTiles, yDistance/yTiles);

			int zoom=0;
			try
			{
				zoom=(int)Math.Floor(Math.Log(360/distancePerTile, 2)); // distancePerTile=(360°/2^Zoom), we round down, to fit all
			}
			catch(OverflowException)
			{
				zoom=100;
			}

			zoom=(zoom<minZoom)?minZoom:zoom; // minimal zoom is 0
			zoom=(zoom>maxZoom)?maxZoom:zoom; // maximal zoom default for osmarender is 18

			return zoom;
		}

		// Calculates the maximum zoom level, with which both coordinates still fit into the given size
		public static int GetOptimalZoomLevel(Size size, BoundingBoxWGS84 box)
		{
			return GetOptimalZoomLevel(size, box.West, box.North, box.East, box.South);
		}

		// Calculates the maximum zoom level, with which both coordinates still fit into the given size
		public static int GetOptimalZoomLevel(Size size, double north, double south)
		{
			return GetOptimalZoomLevel(size, north, south, MinimumZoomLevelOfControl, MaximumZoomLevelOfControl);
		}

		// Calculates the maximum zoom level, with which both coordinates still fit into the given size
		public static int GetOptimalZoomLevel(Size size, double north, double south, int minZoom, int maxZoom)
		{
			double yTiles=(double)size.Height/TileSize.Height;
			double yDistance=north-south;

			double distancePerTile=yDistance/yTiles;

			int zoom=0;
			try
			{
				zoom=(int)Math.Floor(Math.Log(360/distancePerTile, 2)); // distancePerTile=(360°/2^Zoom), we round down, to fit all
			}
			catch(OverflowException)
			{
				zoom=100;
			}

			zoom=(zoom<minZoom)?minZoom:zoom; // minimal zoom is 0
			zoom=(zoom>maxZoom)?maxZoom:zoom; // maximal zoom default for osmarender is 18

			return zoom;
		}
		#endregion

		#region Variables & Enums
		enum Action
		{
			None,
			Move,
			Selection,
			SelectionMove,
			SelectionSizeWest,
			SelectionSizeNorth,
			SelectionSizeEast,
			SelectionSizeSouth,
			ZoomIn,
			ZoomOut,
			ZoomDrag
		}

		Action CurrentAction=Action.None;

		enum HitTestResult
		{
			None,
			Plus,
			Minus,
			ZoomToLevel,
			SelectionMove,
			SelectionDelete,
			SelectionSizeWest,
			SelectionSizeNorth,
			SelectionSizeEast,
			SelectionSizeSouth
		}

		TileManager tm;

		Bitmap osmLogoImage;
		Bitmap zoomPlusImage, zoomMinusImage, zoomDotImage, zoomPosImage;

		double x, y;
		int zoomLevel=0, autoZoomLevel=0, maximumZoomLevel=MaximumZoomLevelOfControl-1;
		int preCacheMaxLevel;

		// Move Variablen
		int moveStartPosX, moveStartPosY;
		int moveDeltaX, moveDeltaY;
		bool wasMoveDrag;

		int selectionStartPosX, selectionStartPosY;
		int selectionDragPosX, selectionDragPosY;

		Rectangle selectionEditPosition;
		Rectangle selectionEditDeleteBox;
		Font selectionXButtonFont, selectionTextFont, scaleTextFont;
		Brush scaleOutLineBrush, scaleLineBrush;
		#endregion

		#region Properties
		[Category("OpenStreetMap Panel"), Description("URL of the tile server.")]
		public string DataProvider
		{
			get { return tm.DataProvider; }
			set { tm.DataProvider=value; }
		}

		[Category("OpenStreetMap Panel"), Description("Sets number of days before the tiles within the cache will be refreshed.")]
		public int DaysBeforeRefresh
		{
			get { return tm.DaysBeforeRefresh; }
			set { tm.DaysBeforeRefresh=value; }
		}

		[Category("OpenStreetMap Panel"), Description("Sets number of tiles cached in memory.")]
		public int TilePoolSize
		{
			get { return tm.TilePoolSize; }
			set { tm.TilePoolSize=value; }
		}

		[Category("OpenStreetMap Panel"), Description("Tile filename extension.")]
		public string FilenameExtension
		{
			get { return tm.FilenameExtension; }
			set { tm.FilenameExtension=value; }
		}

		[Category("OpenStreetMap Panel"), Description("Sets the maximal possible zoom level.")]
		public int MaximumZoomLevel
		{
			get { return maximumZoomLevel; }
			set { maximumZoomLevel=Math.Max(Math.Min(value, MaximumZoomLevelOfControl), MinimumZoomLevelOfControl); }
		}

		[Category("OpenStreetMap Panel"), Description("Show OpenStreetMap Logo and License.")]
		public bool ShowOSMLogoAndLicense { get; set; }

		[Category("OpenStreetMap Panel"), Description("Show scale at the bottom of the map.")]
		public bool ShowScale { get; set; }

		[Category("OpenStreetMap Panel"), Description("Show zoom control.")]
		public bool ShowZoomControl { get; set; }

		[Category("OpenStreetMap Panel"), Description("Activate zoom control.")]
		public bool ZoomControlActive { get; set; }

		[Category("OpenStreetMap Panel"), Description("Sets postion of the zoom control. Negative X values result in right aligning of the control.")]
		public Point ZoomControlPosition { get; set; }

		[Category("OpenStreetMap Panel"), Description("Sets length of the scale control.")]
		public int ScaleControlLength { get; set; }

		[Category("OpenStreetMap Panel"), Description("Sets postion of the zoom control. Negative X values result in right aligning of the control.")]
		public Point ScaleControlPosition { get; set; }

		[Category("OpenStreetMap Panel"), Description("Sets the longitude of the map center.")]
		public double X
		{
			get
			{
				return x;
			}
			set
			{
				x=value;
				while(x>180) x-=360;
				while(x<-180) x+=360;
				Invalidate();
			}
		}

		[Category("OpenStreetMap Panel"), Description("Sets the latitude of the map center.")]
		public double Y
		{
			get
			{
				return y;
			}
			set
			{
				double maxmin=MaxYCoordinate;
				maxmin=Math.Max(0, GetCoordinates(ZoomLevel, 0, (ClientSize.Height/2.0)/TileSize.Height).Y);
				y=Math.Min(Math.Max(value, -maxmin), maxmin);
				Invalidate();
			}
		}

		[Category("OpenStreetMap Panel"), Description("Sets the zoom level of the map.")]
		public int ZoomLevel
		{
			get
			{
				return Math.Max(zoomLevel, autoZoomLevel);
			}
			set
			{
				if(zoomLevel!=value)
				{
					zoomLevel=value;
					if(zoomLevel<MinimumZoomLevelOfControl) zoomLevel=MinimumZoomLevelOfControl;
					if(zoomLevel>maximumZoomLevel) zoomLevel=maximumZoomLevel;
					Invalidate();
					if(ZoomChanged!=null) ZoomChanged(this, Math.Max(zoomLevel, autoZoomLevel));
				}
			}
		}

		[Category("OpenStreetMap Panel"), Description("Set true to zoom to the current mouse position.")]
		public bool ZoomToMousePosition { get; set; }

		[Category("OpenStreetMap Panel"), Description("Set true to zoom in or out on double click with Alt-Key pressed, respectively.")]
		public bool ZoomOnDoubleClick { get; set; }

		[Category("OpenStreetMap Panel"), Description("Set the selection mode:\nNone - deactivates the capabibily to do selections,\nVanish - selection will disappear,\nStay - selection won't disappear,\nEditable - selection can be manipulated.")]
		public SelectionModes SelectionMode { get; set; }

		[Category("OpenStreetMap Panel"), Description("Get the geographic position to the selection.")]
		public BoundingBoxWGS84 Selection { get; set; }

		[Category("OpenStreetMap Panel"), Description("Set true to show the coordinates of the selection inside the selection.")]
		public bool ShowCoordinatesInSelection { get; set; }

		[Category("OpenStreetMap Panel"), Description("Set true activate the pre-caching of tiles.")]
		public bool PreCache { get; set; }

		[Category("OpenStreetMap Panel"), Description("Set max. zoom level for tile pre-caching.")]
		public int PreCacheMaxLevel
		{
			get
			{
				return preCacheMaxLevel;
			}
			set
			{
				preCacheMaxLevel=value;
				if(value<MinimumZoomLevelOfControl) preCacheMaxLevel=MinimumZoomLevelOfControl;
				if(value>maximumZoomLevel) preCacheMaxLevel=maximumZoomLevel;
			}
		}

		// Not Categorized Properties
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Description("Get the geographic longitude to the current mouse position.")]
		public double MouseXWGS84 { get; protected set; }
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[Description("Get the geographic latitude to the current mouse position.")]
		public double MouseYWGS84 { get; protected set; }
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Brush SelectionFillBrush { get; set; }
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Brush SelectionFontBrush { get; set; }
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Pen SelectionOutLinePen { get; set; }
		#endregion

		#region Events
		[Category("OpenStreetMap Panel Events")]
		public event MapPaintedEventHandler MapPainted;

		[Category("OpenStreetMap Panel Events")]
		public event MapPaintedEventHandler SelectionPainted;

		[Category("OpenStreetMap Panel Events")]
		public event PaintEventHandler OverlayPainted;

		[Category("OpenStreetMap Panel Events")]
		public event SelectionChangedHandler SelectionChanged;

		[Category("OpenStreetMap Panel Events")]
		public event PositionChangedHandler PositionChanged;

		[Category("OpenStreetMap Panel Events")]
		public event ZoomChangedHandler ZoomChanged;

		[Category("OpenStreetMap Panel Events")]
		public event ClickHandler NonDragingMouseClick;
		#endregion

		#region Konstruktor
		public OpenStreetMapPanel()
		{
			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			this.SetStyle(ControlStyles.Opaque, true);
			this.SetStyle(ControlStyles.ResizeRedraw, false);
			this.SetStyle(ControlStyles.UserPaint, true);

			tm=new TileManager(this, "http://tile.openstreetmap.org/", 7, 1000, ".png");

			ZoomControlPosition=new Point(30, 30);
			ZoomOnDoubleClick=true;
			SelectionFillBrush=new SolidBrush(Color.FromArgb(63, 127, 127, 255));
			SelectionOutLinePen=Pens.Blue;
			SelectionFontBrush=new SolidBrush(Color.Blue);
			ShowCoordinatesInSelection=true;
			selectionXButtonFont=new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold);
			selectionTextFont=new Font(FontFamily.GenericMonospace, 10, FontStyle.Bold);
			ScaleControlLength=150;
			ScaleControlPosition=new Point(30, 30);
			scaleTextFont=new Font(FontFamily.GenericSerif, 10, FontStyle.Regular);
			scaleOutLineBrush=Brushes.White;
			scaleLineBrush=Brushes.Black;

			osmLogoImage=new Bitmap(GetType(), "osmlogo.png");
			zoomPlusImage=new Bitmap(GetType(), "zoomplus.png");
			zoomMinusImage=new Bitmap(GetType(), "zoomminus.png");
			zoomDotImage=new Bitmap(GetType(), "zoomdot.png");
			zoomPosImage=new Bitmap(GetType(), "zoompos.png");

			InitializeComponent();
		}
		#endregion

		#region Drawing
		// Don't clear the Background before OnPaint.
		protected override void OnPaintBackground(PaintEventArgs e) { }

		protected override void OnPaint(PaintEventArgs e)
		{
			if(this.DesignMode)
			{
				e.Graphics.DrawImageUnscaled(osmLogoImage, (Width-osmLogoImage.Width)/2, (Height-osmLogoImage.Height)/2);
				if(ShowOSMLogoAndLicense) DrawOSMLogoAndLicense(e.Graphics);
				if(ShowScale) DrawScale(e.Graphics);
				if(ShowZoomControl) DrawZoomControl(e.Graphics);
				return;
			}

			base.OnPaint(e); // For the Paint-Events. Even thought they will be painted over.

			autoZoomLevel=GetOptimalZoomLevel(ClientSize, MaxYCoordinate, -MaxYCoordinate);

			PointD tb=GetTileIndex(ZoomLevel, X, Y);
			if(CurrentAction==Action.Move)
			{
				tb.X+=(double)moveDeltaX/TileSize.Width;
				tb.Y+=(double)moveDeltaY/TileSize.Height;
			}

			// Y not outside the imagery
			int z=1<<ZoomLevel;
			double d=(ClientSize.Height/2.0)/TileSize.Height;
			if(tb.Y<d) tb.Y=d;
			if(tb.Y>z-d) tb.Y=z-d;

			double xmin=tb.X-(ClientSize.Width/2.0)/TileSize.Width;
			double xmax=tb.X+(ClientSize.Width/2.0)/TileSize.Width;

			double ymin=tb.Y-(ClientSize.Height/2.0)/TileSize.Height;
			double ymax=tb.Y+(ClientSize.Height/2.0)/TileSize.Height;

			// normalize render coordinates
			while(xmin<0) { xmin+=z; xmax+=z; }
			int maxXRepeats=(int)Math.Ceiling((xmax-xmin)/z)+1;

			int xPixelPosition=(int)((Math.Floor(xmin)-xmin)*TileSize.Width);

			int xminTile=(int)Math.Floor(xmin);
			while(xminTile<=(int)xmax)
			{
				int yPixelPosition=(int)((Math.Floor(ymin)-ymin)*TileSize.Height);

				int yminTile=(int)Math.Floor(ymin);
				while(yminTile<=(int)ymax)
				{
					Bitmap tld=tm.GetTile(ZoomLevel, (xminTile+99*(1<<ZoomLevel))%(1<<ZoomLevel), yminTile);
					e.Graphics.DrawImage(tld, xPixelPosition, yPixelPosition, tld.Width, tld.Height);

					yminTile++;
					yPixelPosition+=TileSize.Height;
				}

				xminTile++;
				xPixelPosition+=TileSize.Width;
			}

			double xscale=ClientSize.Width/(xmax-xmin);
			double yscale=ClientSize.Height/(ymax-ymin);

			if(MapPainted!=null)
			{
				double _xmin=xmin;
				for(int i=0; i<maxXRepeats; i++, _xmin-=z) MapPainted(this, e, _xmin, ymin, xscale, yscale);
			}

			if(CurrentAction==Action.Selection)
			{
				int xmin_=Math.Min(selectionStartPosX, selectionDragPosX);
				int xmax_=Math.Max(selectionStartPosX, selectionDragPosX);
				int ymin_=Math.Min(selectionStartPosY, selectionDragPosY);
				int ymax_=Math.Max(selectionStartPosY, selectionDragPosY);
				e.Graphics.FillRectangle(SelectionFillBrush, xmin_, ymin_, xmax_-xmin_, ymax_-ymin_);
				e.Graphics.DrawRectangle(SelectionOutLinePen, xmin_, ymin_, xmax_-xmin_, ymax_-ymin_);
			}

			if((SelectionMode==SelectionModes.Stay||SelectionMode==SelectionModes.Editable)&&Selection!=null)
			{
				DrawSelection(e.Graphics, xmin, ymin, xscale, yscale, z);
			}

			if(SelectionPainted!=null)
			{
				double _xmin=xmin;
				for(int i=0; i<maxXRepeats; i++, _xmin-=z) SelectionPainted(this, e, _xmin, ymin, xscale, yscale);
			}

			if(ShowOSMLogoAndLicense) DrawOSMLogoAndLicense(e.Graphics);
			if(ShowScale) DrawScale(e.Graphics);
			if(ShowZoomControl) DrawZoomControl(e.Graphics);

			if(OverlayPainted!=null) OverlayPainted(this, e);

			if(!Enabled)
			{
				SolidBrush fillBrush=new SolidBrush(Color.FromArgb(127, 127, 127, 127));
				e.Graphics.FillRectangle(fillBrush, 0, 0, Width, Height);
			}
		}

		void DrawSelection(Graphics g, double xmin, double ymin, double xscale, double yscale, int z)
		{
			PointD nw=GetTileIndex(ZoomLevel, Selection.West, Selection.North);
			PointD se=GetTileIndex(ZoomLevel, Selection.East, Selection.South);
			int xminPixel=(int)(xscale*(nw.X-xmin));
			int xmaxPixel=(int)(xscale*(se.X-xmin));
			int yminPixel=(int)(yscale*(nw.Y-ymin));
			int ymaxPixel=(int)(yscale*(se.Y-ymin));

			// Normalize and find max visibility
			while(xminPixel>Width)
			{
				xminPixel-=z*TileSize.Width;
				xmaxPixel-=z*TileSize.Width;
			}

			while(xmaxPixel<0)
			{
				xminPixel+=z*TileSize.Width;
				xmaxPixel+=z*TileSize.Width;
			}

			if(xminPixel<0)
			{
				int right=Width-(xmaxPixel+z*TileSize.Width);

				if(right>xminPixel)
				{
					xminPixel+=z*TileSize.Width;
					xmaxPixel+=z*TileSize.Width;
				}
			}
			else if(xmaxPixel>Width)
			{
				int left=xminPixel-z*TileSize.Width;
				int right=Width-xmaxPixel;

				if(right<left)
				{
					xminPixel-=z*TileSize.Width;
					xmaxPixel-=z*TileSize.Width;
				}
			}

			bool resetSelection=true;
			if(CurrentAction==Action.SelectionSizeWest||CurrentAction==Action.SelectionSizeNorth||
				CurrentAction==Action.SelectionSizeEast||CurrentAction==Action.SelectionSizeSouth||
				CurrentAction==Action.SelectionMove)
			{
				resetSelection=false;
				xminPixel=selectionEditPosition.Left;
				yminPixel=selectionEditPosition.Top;
				xmaxPixel=selectionEditPosition.Right;
				ymaxPixel=selectionEditPosition.Bottom;

				if(CurrentAction==Action.SelectionMove)
				{
					xminPixel+=selectionDragPosX-selectionStartPosX;
					xmaxPixel+=selectionDragPosX-selectionStartPosX;
					yminPixel+=selectionDragPosY-selectionStartPosY;
					ymaxPixel+=selectionDragPosY-selectionStartPosY;
				}
				else if(CurrentAction==Action.SelectionSizeWest) xminPixel+=selectionDragPosX-selectionStartPosX;
				else if(CurrentAction==Action.SelectionSizeEast) xmaxPixel+=selectionDragPosX-selectionStartPosX;
				else if(CurrentAction==Action.SelectionSizeNorth) yminPixel+=selectionDragPosY-selectionStartPosY;
				else if(CurrentAction==Action.SelectionSizeSouth) ymaxPixel+=selectionDragPosY-selectionStartPosY;

				if(xminPixel>xmaxPixel) { int tmp=xminPixel; xminPixel=xmaxPixel; xmaxPixel=tmp; }
				if(yminPixel>ymaxPixel) { int tmp=yminPixel; yminPixel=ymaxPixel; ymaxPixel=tmp; }
			}

			g.FillRectangle(SelectionFillBrush, xminPixel, yminPixel, xmaxPixel-xminPixel, ymaxPixel-yminPixel);
			g.DrawRectangle(SelectionOutLinePen, xminPixel, yminPixel, xmaxPixel-xminPixel, ymaxPixel-yminPixel);

			if(ShowCoordinatesInSelection&&xmaxPixel-xminPixel>260&&ymaxPixel-yminPixel>35)
			{
				string NW=dtodms(Selection.North, 'N', 'S')+" "+dtodms(Selection.West, 'W', 'E');
				string SE=dtodms(Selection.South, 'N', 'S')+" "+dtodms(Selection.East, 'W', 'E');
				g.DrawString(NW, selectionTextFont, SelectionFontBrush, xminPixel+2, yminPixel+2);
				g.DrawString(SE, selectionTextFont, SelectionFontBrush, xmaxPixel-4-(int)(SE.Length*8.25), ymaxPixel-16);
			}

			if(SelectionMode==SelectionModes.Editable)
			{
				if(resetSelection) selectionEditPosition=new Rectangle(xminPixel, yminPixel, xmaxPixel-xminPixel, ymaxPixel-yminPixel);

				if(xmaxPixel-xminPixel<25||ymaxPixel-yminPixel<25)
				{
					if((xmaxPixel+15>Width)&&(xminPixel-15>=0))
					{
						selectionEditDeleteBox=new Rectangle(xminPixel-15, yminPixel-15, 10, 10);
						g.DrawLine(SelectionOutLinePen, xminPixel, yminPixel, xminPixel-5, yminPixel-5);
						g.FillRectangle(SelectionFillBrush, selectionEditDeleteBox);
						g.DrawRectangle(SelectionOutLinePen, selectionEditDeleteBox);
						g.DrawString("x", selectionXButtonFont, SelectionFontBrush, xminPixel-15, yminPixel-19);
					}
					else
					{
						selectionEditDeleteBox=new Rectangle(xmaxPixel+5, yminPixel-15, 10, 10);
						g.DrawLine(SelectionOutLinePen, xmaxPixel, yminPixel, xmaxPixel+5, yminPixel-5);
						g.FillRectangle(SelectionFillBrush, selectionEditDeleteBox);
						g.DrawRectangle(SelectionOutLinePen, selectionEditDeleteBox);
						g.DrawString("x", selectionXButtonFont, SelectionFontBrush, xmaxPixel+5, yminPixel-19);
					}
				}
				else
				{
					if(((xmaxPixel-5>Width)&&(xminPixel+5>=0))||((xmaxPixel-10>Width)&&(xminPixel+15>=0)))
					{
						selectionEditDeleteBox=new Rectangle(xminPixel+5, yminPixel+5, 10, 10);
						g.DrawRectangle(SelectionOutLinePen, selectionEditDeleteBox);
						g.DrawString("x", selectionXButtonFont, SelectionFontBrush, xminPixel+5, yminPixel+1);
					}
					else
					{
						selectionEditDeleteBox=new Rectangle(xmaxPixel-15, yminPixel+5, 10, 10);
						g.DrawRectangle(SelectionOutLinePen, selectionEditDeleteBox);
						g.DrawString("x", selectionXButtonFont, SelectionFontBrush, xmaxPixel-15, yminPixel+1);
					}
				}
			}
		}

		void DrawOSMLogoAndLicense(Graphics g)
		{
			string license="© OpenStreetMap contributors";
			SizeF size=g.MeasureString(license, SystemFonts.DialogFont);

			using(Brush brush=new SolidBrush(Color.FromArgb(178, Color.White)))
				g.FillRectangle(brush, ClientSize.Width-size.Width-4, ClientSize.Height-size.Height, size.Width+4, size.Height);

			using(Brush brush=new SolidBrush(Color.FromArgb(200, Color.Black)))
				g.DrawString(license, SystemFonts.DialogFont, brush, ClientSize.Width-size.Width-2, ClientSize.Height-size.Height);

			g.DrawImage(osmLogoImage, ClientSize.Width-64, ClientSize.Height-64-size.Height, osmLogoImage.Width, osmLogoImage.Height);
		}

		void DrawScale(Graphics g)
		{
			double s=2*Math.PI*6378137*Math.Cos(Y*Math.PI/180)/(1<<ZoomLevel);

			int maxScaleSize=Math.Abs(ScaleControlLength);
			if(maxScaleSize<30) return;
			double maxScaleValue=s*maxScaleSize/TileSize.Width;

			int zeros=(int)Math.Log10(maxScaleValue);
			double scaleValue=maxScaleValue/Math.Pow(10, zeros);

			bool km=false;
			if(zeros>=3)
			{
				km=true;
				zeros-=3;
			}

			int newScaleValue=1;
			if(scaleValue>=5) newScaleValue=5;
			else if(scaleValue>=2) newScaleValue=2;

			newScaleValue*=(int)(Math.Pow(10, zeros)+0.5);

			int scalelength=(int)(maxScaleSize*(km?1000.0:1.0)*newScaleValue/maxScaleValue+0.5);

			int posx=ScaleControlPosition.X;
			int posy=Math.Abs(ScaleControlPosition.Y);

			if(posx<0) posx+=Width-scalelength-2;

			if(posx+scalelength+3>Width-Math.Abs(ScaleControlPosition.X)) return;

			g.FillRectangle(scaleOutLineBrush, posx, Height-posy+14, scalelength+3, 4);
			g.FillRectangle(scaleOutLineBrush, posx, Height-posy, 4, 14);
			g.FillRectangle(scaleOutLineBrush, posx-1+scalelength, Height-posy, 4, 14);

			g.FillRectangle(scaleLineBrush, posx+1, Height-posy+15, scalelength+1, 2);
			g.FillRectangle(scaleLineBrush, posx+1, Height-posy+1, 2, 14);
			g.FillRectangle(scaleLineBrush, posx+scalelength, Height-posy+1, 2, 14);

			g.DrawString(newScaleValue.ToString()+(km?" km":" m"), scaleTextFont, scaleLineBrush, posx+4, Height-posy);
		}

		void DrawZoomControl(Graphics g)
		{
			int zoomControlHeight=zoomPlusImage.Height+zoomMinusImage.Height+zoomDotImage.Height*(maximumZoomLevel-MinimumZoomLevelOfControl+1);
			int zoomPaintPosX=ZoomControlPosition.X;
			if(ZoomControlPosition.X<0) zoomPaintPosX=Width+ZoomControlPosition.X-zoomDotImage.Width;
			int zoomPaintPosY=ZoomControlPosition.Y;
			if(Height<zoomPaintPosY*2+zoomControlHeight) zoomPaintPosY=(Height-zoomControlHeight)/2;

			if(zoomPaintPosY<=0) return;

			g.DrawImageUnscaled(zoomPlusImage, zoomPaintPosX, zoomPaintPosY);
			zoomPaintPosY+=zoomPlusImage.Height;

			for(int i=maximumZoomLevel; i>=MinimumZoomLevelOfControl; i--)
			{
				if(ZoomLevel==i)
				{
					g.DrawImageUnscaled(zoomPosImage, zoomPaintPosX, zoomPaintPosY);
					zoomPaintPosY+=zoomPosImage.Height;
				}
				else
				{
					g.DrawImageUnscaled(zoomDotImage, zoomPaintPosX, zoomPaintPosY);
					zoomPaintPosY+=zoomDotImage.Height;
				}
			}

			g.DrawImageUnscaled(zoomMinusImage, zoomPaintPosX, zoomPaintPosY);
		}
		#endregion

		#region MouseCenteredZoom
		void ZoomIn(MouseEventArgs e)
		{
			double mX=0, mY=0;
			if(ZoomToMousePosition)
			{
				if(e.X>=0&&e.Y>=0&&e.X<Width&&e.Y<Height)
				{
					mX=MouseXWGS84;
					mY=MouseYWGS84;
				}
			}

			ZoomLevel+=1;

			if(ZoomToMousePosition)
			{
				if(e.X>=0&&e.Y>=0&&e.X<Width&&e.Y<Height)
				{
					double offsetX=Width/2.0-e.X;
					double offsetY=Height/2.0-e.Y;

					PointD coord=GetTileIndex(ZoomLevel, mX, mY);
					PointD newCenter=GetCoordinates(ZoomLevel, coord.X+offsetX/TileSize.Width, coord.Y+offsetY/TileSize.Height);
					X=newCenter.X;
					Y=newCenter.Y;
					if(PositionChanged!=null) PositionChanged(this, X, Y);
				}
			}
		}

		void ZoomOut(MouseEventArgs e)
		{
			double mX=0, mY=0;
			if(ZoomToMousePosition)
			{
				if(e.X>=0&&e.Y>=0&&e.X<Width&&e.Y<Height)
				{
					mX=MouseXWGS84;
					mY=MouseYWGS84;
				}
			}

			ZoomLevel-=1;

			if(ZoomToMousePosition)
			{
				if(e.X>=0&&e.Y>=0&&e.X<Width&&e.Y<Height)
				{
					double offsetX=Width/2.0-e.X;
					double offsetY=Height/2.0-e.Y;

					PointD coord=GetTileIndex(ZoomLevel, mX, mY);
					PointD newCenter=GetCoordinates(ZoomLevel, coord.X+offsetX/TileSize.Width, coord.Y+offsetY/TileSize.Height);
					X=newCenter.X;
					Y=newCenter.Y;
					if(PositionChanged!=null) PositionChanged(this, X, Y);
				}
			}
		}
		#endregion

		#region Mouse-Tests and -Events
		HitTestResult HitTest(int x, int y)
		{
			while(ZoomControlActive&&ShowZoomControl)
			{
				int xmin=ZoomControlPosition.X;
				if(ZoomControlPosition.X<0) xmin=Width+ZoomControlPosition.X-zoomDotImage.Width;

				int xmax=xmin+zoomPlusImage.Width;

				if(x<xmin||x>xmax) break;

				int ymin=ZoomControlPosition.Y;
				if(y<ymin) break;
				ymin+=zoomPlusImage.Height;
				if(y<ymin) return HitTestResult.Plus;

				for(int i=MinimumZoomLevelOfControl; i<=maximumZoomLevel; i++)
				{
					ymin+=zoomDotImage.Height;
					if(y<ymin) return HitTestResult.ZoomToLevel;
				}

				ymin+=zoomMinusImage.Height;
				if(y<ymin) return HitTestResult.Minus;
				break;
			}

			while(Selection!=null&&SelectionMode==SelectionModes.Editable)
			{
				Rectangle mousePosition=new Rectangle(x, y, 1, 1);
				if(selectionEditDeleteBox.IntersectsWith(mousePosition)) return HitTestResult.SelectionDelete;

				Rectangle selectionEditPositionGrown=selectionEditPosition;
				selectionEditPositionGrown.Inflate(1, 1);
				if(selectionEditPositionGrown.IntersectsWith(mousePosition))
				{
					int north=selectionEditPosition.Top;
					if(north-1<=y&&north+1>=y) return HitTestResult.SelectionSizeNorth;
					int south=selectionEditPosition.Bottom;
					if(south-1<=y&&south+1>=y) return HitTestResult.SelectionSizeSouth;
					int west=selectionEditPosition.Left;
					if(west-1<=x&&west+1>=x) return HitTestResult.SelectionSizeWest;
					int east=selectionEditPosition.Right;
					if(east-1<=x&&east+1>=x) return HitTestResult.SelectionSizeEast;
					return HitTestResult.SelectionMove;
				}
				break;
			}

			return HitTestResult.None;
		}

		int HitTestZoomToLevel(int x, int y)
		{
			int ymin=ZoomControlPosition.Y;

			int xmin=ZoomControlPosition.X;
			if(ZoomControlPosition.X<0) xmin=Width+ZoomControlPosition.X-zoomDotImage.Width;

			int xmax=xmin+zoomPlusImage.Width;
			if(x<xmin||x>xmax) return -1;

			if(y<ymin) return -1;
			ymin+=zoomPlusImage.Height;
			if(y<ymin) return -1;

			for(int i=maximumZoomLevel; i>=MinimumZoomLevelOfControl; i--)
			{
				ymin+=zoomDotImage.Height;
				if(y<ymin) return i+MinimumZoomLevelOfControl;
			}

			return -1;
		}

		void MapPanel_MouseWheel(object sender, MouseEventArgs e)
		{
			if(CurrentAction!=Action.None) return;

			if(e.Delta==0) return;

			if(e.Delta>0) ZoomIn(e);
			if(e.Delta<0) ZoomOut(e);

			Invalidate();
		}

		private void MapPanel_MouseMove(object sender, MouseEventArgs e)
		{
			switch(CurrentAction)
			{
				case Action.Move:
					moveDeltaX=moveStartPosX-e.X;
					moveDeltaY=moveStartPosY-e.Y;

					if(Math.Abs(moveDeltaX)>0||Math.Abs(moveDeltaY)>0) wasMoveDrag=true;

					Invalidate();
					break;
				case Action.Selection:
				case Action.SelectionMove:
				case Action.SelectionSizeWest:
				case Action.SelectionSizeNorth:
				case Action.SelectionSizeEast:
				case Action.SelectionSizeSouth:
					selectionDragPosX=e.X;
					selectionDragPosY=e.Y;
					Invalidate();
					break;
				default: break;
			}

			// first Mouse
			PointD newMouseTileCoord=GetTileIndex(ZoomLevel, X, Y);
			newMouseTileCoord.X+=(moveDeltaX+e.X-ClientSize.Width/2.0)/TileSize.Width;
			newMouseTileCoord.Y+=(moveDeltaY+e.Y-ClientSize.Height/2.0)/TileSize.Height;
			PointD newMouseCoord=GetCoordinates(ZoomLevel, newMouseTileCoord.X, newMouseTileCoord.Y);
			MouseXWGS84=newMouseCoord.X;
			MouseYWGS84=newMouseCoord.Y;

			// then Event
			if(CurrentAction==Action.Move&&PositionChanged!=null)
			{
				PointD newPositionTileCoord=GetTileIndex(ZoomLevel, X, Y);
				newPositionTileCoord.X+=(double)moveDeltaX/TileSize.Width;
				newPositionTileCoord.Y+=(double)moveDeltaY/TileSize.Height;
				PointD newPositionCoord=GetCoordinates(ZoomLevel, newPositionTileCoord.X, newPositionTileCoord.Y);
				PositionChanged(this, newPositionCoord.X, newPositionCoord.Y);
			}

			if(CurrentAction==Action.None)
			{
				Cursor=Cursors.Default;
				HitTestResult res=HitTest(e.X, e.Y);
				switch(res)
				{
					default:
					case HitTestResult.None: Cursor=Cursors.Default; break;
					case HitTestResult.Plus:
					case HitTestResult.Minus:
					case HitTestResult.ZoomToLevel: Cursor=Cursors.Hand; break;
					case HitTestResult.SelectionMove: Cursor=Cursors.SizeAll; break;
					case HitTestResult.SelectionSizeWest: Cursor=Cursors.VSplit; break;
					case HitTestResult.SelectionSizeNorth: Cursor=Cursors.HSplit; break;
					case HitTestResult.SelectionSizeEast: Cursor=Cursors.VSplit; break;
					case HitTestResult.SelectionSizeSouth: Cursor=Cursors.HSplit; break;
					case HitTestResult.SelectionDelete: Cursor=Cursors.Hand; break;
				}
			}
		}

		private void MapPanel_MouseDown(object sender, MouseEventArgs e)
		{
			if(e.Button!=MouseButtons.Left) return;

			wasMoveDrag=false;
			HitTestResult res=HitTest(e.X, e.Y);
			switch(res)
			{
				case HitTestResult.None:
					if(SelectionMode!=SelectionModes.None&&ModifierKeys==Keys.Shift)
					{
						CurrentAction=Action.Selection;
						selectionDragPosX=selectionDragPosY=0;
						selectionStartPosX=e.X;
						selectionStartPosY=e.Y;
					}
					else
					{
						CurrentAction=Action.Move;
						moveStartPosX=e.X;
						moveStartPosY=e.Y;
					}
					Cursor=Cursors.Default;
					break;
				case HitTestResult.Plus: ZoomLevel+=1; Cursor=Cursors.Hand; break;
				case HitTestResult.Minus: ZoomLevel-=1; Cursor=Cursors.Hand; break;
				case HitTestResult.ZoomToLevel:
					{
						int newZoomLevel=HitTestZoomToLevel(e.X, e.Y);
						if(newZoomLevel!=-1) ZoomLevel=newZoomLevel;
						Cursor=Cursors.Hand;
					}
					break;
				case HitTestResult.SelectionMove:
					CurrentAction=Action.SelectionMove;
					selectionStartPosX=e.X;
					selectionStartPosY=e.Y;
					Cursor=Cursors.SizeAll;
					break;
				case HitTestResult.SelectionSizeWest:
					CurrentAction=Action.SelectionSizeWest;
					selectionStartPosX=e.X;
					Cursor=Cursors.VSplit;
					break;
				case HitTestResult.SelectionSizeNorth:
					CurrentAction=Action.SelectionSizeNorth;
					selectionStartPosY=e.Y;
					Cursor=Cursors.HSplit;
					break;
				case HitTestResult.SelectionSizeEast:
					CurrentAction=Action.SelectionSizeEast;
					selectionStartPosX=e.X;
					Cursor=Cursors.VSplit;
					break;
				case HitTestResult.SelectionSizeSouth:
					CurrentAction=Action.SelectionSizeSouth;
					selectionStartPosY=e.Y;
					Cursor=Cursors.HSplit;
					break;
				case HitTestResult.SelectionDelete:
					Selection=null;
					Cursor=Cursors.Hand;
					Invalidate();
					break;
			}
		}

		private void MapPanel_MouseUp(object sender, MouseEventArgs e)
		{
			if(e.Button!=MouseButtons.Left) return;

			switch(CurrentAction)
			{
				case Action.Selection:
					{
						PointD centerTileCoord=GetTileIndex(ZoomLevel, X, Y);

						PointD startMouseTileCoord=centerTileCoord;
						startMouseTileCoord.X+=(selectionStartPosX-ClientSize.Width/2.0)/TileSize.Width;
						startMouseTileCoord.Y+=(selectionStartPosY-ClientSize.Height/2.0)/TileSize.Height;

						PointD endMouseTileCoord=centerTileCoord;
						endMouseTileCoord.X+=(e.X-ClientSize.Width/2.0)/TileSize.Width;
						endMouseTileCoord.Y+=(e.Y-ClientSize.Height/2.0)/TileSize.Height;

						PointD startMouseCoord=GetCoordinates(ZoomLevel, startMouseTileCoord.X, startMouseTileCoord.Y);
						PointD endMouseCoord=GetCoordinates(ZoomLevel, endMouseTileCoord.X, endMouseTileCoord.Y);
						MouseXWGS84=endMouseCoord.X;
						MouseYWGS84=endMouseCoord.Y;

						Selection=new BoundingBoxWGS84(startMouseCoord.X, startMouseCoord.Y, endMouseCoord.X, endMouseCoord.Y, MaxYCoordinate);

						CurrentAction=Action.None;
						selectionStartPosX=selectionStartPosY=0;
						selectionDragPosX=selectionDragPosY=0;

						Invalidate();
						if(SelectionChanged!=null) SelectionChanged(this, Selection);
					}
					break;
				case Action.Move:
					{
						moveDeltaX=moveStartPosX-e.X;
						moveDeltaY=moveStartPosY-e.Y;

						PointD tb=GetTileIndex(ZoomLevel, X, Y);
						tb.X+=(double)moveDeltaX/TileSize.Width;
						tb.Y+=(double)moveDeltaY/TileSize.Height;

						PointD coord=GetCoordinates(ZoomLevel, tb.X, tb.Y);
						X=coord.X;
						Y=coord.Y;

						CurrentAction=Action.None;
						moveStartPosX=moveStartPosY=0;
						moveDeltaX=moveDeltaY=0;

						Invalidate();
						if(PositionChanged!=null) PositionChanged(this, X, Y);

						if(!wasMoveDrag&&NonDragingMouseClick!=null) NonDragingMouseClick(this, MouseXWGS84, MouseYWGS84);
					}
					break;
				case Action.SelectionMove:
				case Action.SelectionSizeWest:
				case Action.SelectionSizeNorth:
				case Action.SelectionSizeEast:
				case Action.SelectionSizeSouth:
					{
						PointD selectionNWTileCoord=GetTileIndex(ZoomLevel, Selection.West, Selection.North);
						PointD selectionSETileCoord=GetTileIndex(ZoomLevel, Selection.East, Selection.South);

						PointD startMouseTileCoord=selectionNWTileCoord;
						if(CurrentAction==Action.SelectionSizeWest||CurrentAction==Action.SelectionMove)
							startMouseTileCoord.X+=((double)e.X-selectionStartPosX)/TileSize.Width;
						if(CurrentAction==Action.SelectionSizeNorth||CurrentAction==Action.SelectionMove)
							startMouseTileCoord.Y+=((double)e.Y-selectionStartPosY)/TileSize.Height;

						PointD endMouseTileCoord=selectionSETileCoord;
						if(CurrentAction==Action.SelectionSizeEast||CurrentAction==Action.SelectionMove)
							endMouseTileCoord.X+=((double)e.X-selectionStartPosX)/TileSize.Width;
						if(CurrentAction==Action.SelectionSizeSouth||CurrentAction==Action.SelectionMove)
							endMouseTileCoord.Y+=((double)e.Y-selectionStartPosY)/TileSize.Height;

						PointD startMouseCoord=GetCoordinates(ZoomLevel, startMouseTileCoord.X, startMouseTileCoord.Y);
						PointD endMouseCoord=GetCoordinates(ZoomLevel, endMouseTileCoord.X, endMouseTileCoord.Y);

						PointD mouseCoord=GetCoordinates(ZoomLevel, e.X, e.Y);
						MouseXWGS84=mouseCoord.X;
						MouseYWGS84=mouseCoord.Y;

						Selection=new BoundingBoxWGS84(startMouseCoord.X, startMouseCoord.Y, endMouseCoord.X, endMouseCoord.Y, MaxYCoordinate);

						CurrentAction=Action.None;
						selectionStartPosX=selectionStartPosY=0;
						selectionDragPosX=selectionDragPosY=0;

						Invalidate();
						if(SelectionChanged!=null) SelectionChanged(this, Selection);
					}
					break;
			}
		}

		private void MapPanel_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if(ZoomOnDoubleClick&&e.Button==MouseButtons.Left)
			{
				if(ModifierKeys!=Keys.Alt) ZoomIn(e);
				else ZoomOut(e);

				Invalidate();
			}
		}
		#endregion

		#region Control Events
		private void MapPanel_Load(object sender, EventArgs e)
		{
			if(PreCache) tm.DownloadCache(PreCacheMaxLevel);
		}

		private void MapPanel_Resize(object sender, EventArgs e)
		{
			Invalidate();
		}
		#endregion

		#region Methods
		public void ZoomToBoundingBox(BoundingBoxWGS84 bbox)
		{
			if(bbox==null) return;
			int zoomLevel=GetOptimalZoomLevel(ClientSize, bbox);
			ZoomLevel=zoomLevel-1;

			X=(bbox.West+bbox.East)/2;
			Y=(bbox.North+bbox.South)/2;

			if(PositionChanged!=null) PositionChanged(this, X, Y);
		}

		public void ZoomToSelection()
		{
			if(Selection==null) return;
			int zoomLevel=GetOptimalZoomLevel(ClientSize, Selection);
			ZoomLevel=zoomLevel-1;

			X=(Selection.West+Selection.East)/2;
			Y=(Selection.North+Selection.South)/2;

			if(PositionChanged!=null) PositionChanged(this, X, Y);
		}

		public PointD GetPixelPosition(double x, double y)
		{
			PointD ret=OpenStreetMapPanel.GetTileIndex(ZoomLevel, x, y);
			PointD centerTileCoord=GetTileIndex(ZoomLevel, X, Y);

			// Add deltas when moving.
			if(CurrentAction==Action.Move)
			{
				centerTileCoord.X+=(double)moveDeltaX/TileSize.Width;
				centerTileCoord.Y+=(double)moveDeltaY/TileSize.Height;
			}

			ret.X-=centerTileCoord.X;
			ret.Y-=centerTileCoord.Y;

			ret.X*=TileSize.Width;
			ret.Y*=TileSize.Height;

			// To get the pixel position closest to the control center,
			// if multiple positions are posible causeed by a low zoomlevel.
			int z=TileSize.Width<<ZoomLevel;
			if(ret.X<0)
			{
				if(Math.Abs(ret.X)>Math.Abs(ret.X+z)) ret.X+=z;
			}
			else if(ret.X>=0)
			{
				if(Math.Abs(ret.X)>Math.Abs(ret.X-z)) ret.X-=z;
			}

			ret.X+=ClientSize.Width/2.0;
			ret.Y+=ClientSize.Height/2.0;

			return ret;
		}
		#endregion

		public static string dtodms(double d, char pos, char neg)
		{
			// RES is fractional second figures
			// RES60 = 60 * RES
			// CONV = 3600 * RES
			double RES=1000.0, RES60=60000.0, CONV=3600000.0;
			string sign="";
			int deg, min;
			double sec;

			string ss="";

			if(d<0)
			{
				d=-d;
				if(pos!='\0'&&neg!='\0') sign+=neg;
				else ss+='-';
			}
			else
			{
				if(pos!='\0') sign+=pos;
			}

			d=Math.Floor(d*CONV+0.5);
			sec=(d/RES)%60.0;
			d=Math.Floor(d/RES60);
			min=(int)(d%60.0);
			d=Math.Floor(d/60.0);
			deg=(int)d;

			if(sec!=0.0) ss+=string.Format(nc, "{0}°{1}'{2:0.###}\"{3}", deg, min, sec, sign);
			else if(min!=0) ss+=string.Format(nc, "{0}°{1}'{2}", deg, min, sign);
			else ss+=string.Format(nc, "{0}°{1}", deg, sign);

			return ss;
		}
	}
}
