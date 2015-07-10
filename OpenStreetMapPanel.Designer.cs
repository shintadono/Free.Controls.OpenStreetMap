namespace Free.Controls.OpenStreetMap
{
	partial class OpenStreetMapPanel
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
			if (disposing&&(components!=null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.SuspendLayout();
			// 
			// MapPanel
			// 
			this.AutoScaleDimensions=new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode=System.Windows.Forms.AutoScaleMode.Font;
			this.Name="MapPanel";
			this.Size=new System.Drawing.Size(615, 400);
			this.Load+=new System.EventHandler(this.MapPanel_Load);
			this.MouseWheel+=new System.Windows.Forms.MouseEventHandler(this.MapPanel_MouseWheel);
			this.MouseMove+=new System.Windows.Forms.MouseEventHandler(this.MapPanel_MouseMove);
			this.MouseDoubleClick+=new System.Windows.Forms.MouseEventHandler(this.MapPanel_MouseDoubleClick);
			this.MouseDown+=new System.Windows.Forms.MouseEventHandler(this.MapPanel_MouseDown);
			this.Resize+=new System.EventHandler(this.MapPanel_Resize);
			this.MouseUp+=new System.Windows.Forms.MouseEventHandler(this.MapPanel_MouseUp);
			this.ResumeLayout(false);

		}
		#endregion
	}
}
