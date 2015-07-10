namespace Free.Controls.OpenStreetMap
{
	partial class PositionPickerDialog
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components=null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if(disposing&&(components!=null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PositionPickerDialog));
			this.buttonOK = new System.Windows.Forms.Button();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.textBoxPosition = new System.Windows.Forms.TextBox();
			this.mapPanel = new Free.Controls.OpenStreetMap.OpenStreetMapPanel();
			this.SuspendLayout();
			// 
			// buttonOK
			// 
			resources.ApplyResources(this.buttonOK, "buttonOK");
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.UseVisualStyleBackColor = true;
			this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
			// 
			// buttonCancel
			// 
			resources.ApplyResources(this.buttonCancel, "buttonCancel");
			this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
			// 
			// textBoxPosition
			// 
			resources.ApplyResources(this.textBoxPosition, "textBoxPosition");
			this.textBoxPosition.Name = "textBoxPosition";
			this.textBoxPosition.ReadOnly = true;
			// 
			// mapPanel
			// 
			resources.ApplyResources(this.mapPanel, "mapPanel");
			this.mapPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.mapPanel.DataProvider = "http://tile.openstreetmap.org/";
			this.mapPanel.DaysBeforeRefresh = 7;
			this.mapPanel.FilenameExtension = ".png";
			this.mapPanel.MaximumZoomLevel = 17;
			this.mapPanel.Name = "mapPanel";
			this.mapPanel.PreCache = false;
			this.mapPanel.PreCacheMaxLevel = 0;
			this.mapPanel.ScaleControlLength = 150;
			this.mapPanel.ScaleControlPosition = new System.Drawing.Point(16, 32);
			this.mapPanel.Selection = null;
			this.mapPanel.SelectionMode = Free.Controls.OpenStreetMap.SelectionModes.None;
			this.mapPanel.ShowCoordinatesInSelection = true;
			this.mapPanel.ShowOSMLogoAndLicense = true;
			this.mapPanel.ShowScale = true;
			this.mapPanel.ShowZoomControl = true;
			this.mapPanel.TilePoolSize = 1000;
			this.mapPanel.X = 0D;
			this.mapPanel.Y = 0D;
			this.mapPanel.ZoomControlActive = true;
			this.mapPanel.ZoomControlPosition = new System.Drawing.Point(16, 16);
			this.mapPanel.ZoomLevel = 0;
			this.mapPanel.ZoomOnDoubleClick = false;
			this.mapPanel.ZoomToMousePosition = true;
			this.mapPanel.MapPainted += new Free.Controls.OpenStreetMap.MapPaintedEventHandler(this.mapPanel_MapPainted);
			this.mapPanel.NonDragingMouseClick += new Free.Controls.OpenStreetMap.ClickHandler(this.mapPanel_NonDragingMouseClick);
			this.mapPanel.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.mapPanel_MouseDoubleClick);
			// 
			// PositionPickerDialog
			// 
			this.AcceptButton = this.buttonOK;
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.buttonCancel;
			this.Controls.Add(this.textBoxPosition);
			this.Controls.Add(this.buttonCancel);
			this.Controls.Add(this.buttonOK);
			this.Controls.Add(this.mapPanel);
			this.Name = "PositionPickerDialog";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenStreetMapPanel mapPanel;
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.TextBox textBoxPosition;
	}
}
