using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Free.Controls.OpenStreetMap
{
	class TileManager
	{
		string cacheDirectory=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)+"\\Free Framework\\OpenStreetMap\\Cache";

		TimeSpan refreshTimeSpan=new TimeSpan(7, 0, 1, 0);
		BitmapPooledLoader pooledLoader;
		Bitmap errorImage;

		public Control HostMapPanel { get; set; }
		public string DataProvider { get; set; }
		public int TilePoolSize
		{
			get { return (int)pooledLoader.MaxImages; }
			set { pooledLoader.MaxImages=(uint)Math.Abs(value); }
		}
		public string FilenameExtension { get; set; }
		public int DaysBeforeRefresh
		{
			get { return refreshTimeSpan.Days; }
			set { refreshTimeSpan=new TimeSpan(Math.Abs(value), 0, 1, 0); }
		}

		public TileManager(Control mapPanel, string dataProvider): this(mapPanel, dataProvider, 7, 1000, ".png") { }

		public TileManager(Control mapPanel, string dataProvider, int daysBeforeRefresh, uint tilePoolSize, string filenameextension)
		{
			errorImage=new Bitmap(GetType(), "raster.png");

			if(!Directory.Exists(cacheDirectory))
			{
				char[] sep=new char[] { '/', '\\' };
				string[] pathParts=cacheDirectory.Split(sep, StringSplitOptions.RemoveEmptyEntries);

				string path="";
				foreach(string i in pathParts)
				{
					path+=i+"\\";
					if(path.Length<=3) continue; // root (C:\)

					if(!Directory.Exists(path))
					{
						try { Directory.CreateDirectory(path); }
						catch { }
					}
				}
			}

			string[] tmps=GetFiles(cacheDirectory, "*.tmp");

			foreach(string i in tmps)
			{
				try
				{
					File.Delete(i);
				}
				catch
				{
				}
			}

			HostMapPanel=mapPanel;
			DataProvider=dataProvider;
			refreshTimeSpan=new TimeSpan(daysBeforeRefresh, 0, 1, 0);
			pooledLoader=new BitmapPooledLoader(tilePoolSize);
			FilenameExtension=filenameextension;
		}

		public void ClearCache()
		{
			string[] tmps=GetFiles(cacheDirectory, FilenameExtension);
			foreach(string i in tmps) File.Delete(i);
		}

		public void DownloadCache(int maxlevel)
		{
			for(int i=0; i<maxlevel; i++)
			{
				int maxTilesDimension=1<<i;

				for(int x=0; x<maxTilesDimension; x++)
				{
					for(int y=0; y<maxTilesDimension; y++)
					{
						string fn=GetTileFilename(i, x, y);
						if(File.Exists(fn))
						{
							// determine file age
							if(DateTime.Now-refreshTimeSpan>new FileInfo(fn).LastWriteTime)
							{
								// check if file already in queue
								if(!File.Exists(fn+".tmp")) DownloadTileAsync(i, x, y); // refresh
							}
							continue;
						}

						if(!File.Exists(fn+".tmp")) DownloadTileAsync(i, x, y);
					}
				}
			}
		}

		public Bitmap GetTile(int zoom, int x, int y)
		{
			if(x<0||x>=Math.Pow(2, zoom)||y<0||y>=Math.Pow(2, zoom)) return errorImage;

			string fn=GetTileFilename(zoom, x, y);

			if(File.Exists(fn))
			{
				// determine file age
				if(DateTime.Now-refreshTimeSpan>new FileInfo(fn).LastWriteTime)
				{
					// check if file already in queue
					if(!File.Exists(fn+".tmp"))
					{
						DownloadTileAsync(zoom, x, y); // refresh
					}
				}

				return pooledLoader.FromFile(fn)??errorImage;
			}

			if(!File.Exists(fn+".tmp")) DownloadTileAsync(zoom, x, y);
			if(zoom==0) return errorImage;

			Bitmap zoomOut=GetTile(zoom-1, x/2, y/2);
			Bitmap ret=new Bitmap(zoomOut.Width, zoomOut.Height);

			Graphics g=null;

			try
			{
				g=Graphics.FromImage(ret);
				g.Clear(Color.Transparent);
				int posx=-(x%2)*zoomOut.Width;
				int posy=-(y%2)*zoomOut.Height;
				g.DrawImage(zoomOut, posx, posy, zoomOut.Width*2+1, zoomOut.Height*2+1);
			}
			finally
			{
				if(g!=null) g.Dispose();
			}

			return ret;
		}

		void ClientDownloadFileAsyncHandler(object sender, AsyncCompletedEventArgs e)
		{
			string filename=e.UserState as string;
			if(filename==null) return;

			if(e.Error!=null)
			{
				try { File.Delete(filename); }
				catch { }
				return;
			}

			string newfilename=GetPathFilenameWithoutExt(filename);

			if(File.Exists(newfilename))
			{
				try { File.Delete(newfilename); }
				catch { }
				return;
			}

			try { File.Move(filename, newfilename); }
			catch { }

			if(HostMapPanel!=null) HostMapPanel.Invalidate();
		}

		public void DownloadTileAsync(int zoom, int x, int y)
		{
			var t=Task.Factory.StartNew(() => DownloadTile(zoom, x, y));
		}

		void DownloadTile(int zoom, int x, int y)
		{
			if(x<0||x>=Math.Pow(2, zoom)||y<0||y>=Math.Pow(2, zoom)) return;

			try
			{
				WebClient client=new WebClient();
				client.DownloadFileCompleted+=new System.ComponentModel.AsyncCompletedEventHandler(ClientDownloadFileAsyncHandler);
				string fn=GetTileFilename(zoom, x, y)+".tmp";
				client.DownloadFileAsync(new Uri(DataProvider+zoom+"/"+x+"/"+y+FilenameExtension), fn, fn);
			}
			catch
			{
			}
		}

		string GetTileFilename(int zoom, int x, int y)
		{
			return cacheDirectory+"\\"+zoom+"_"+x+"_"+y+FilenameExtension;
		}

		static string GetPathFilenameWithoutExt(string filename)
		{
			FileInfo ret=new FileInfo(filename);
			if(ret.DirectoryName.Length==3) return ret.DirectoryName+ret.Name.Substring(0, ret.Name.Length-ret.Extension.Length);
			return ret.DirectoryName+'\\'+ret.Name.Substring(0, ret.Name.Length-ret.Extension.Length);
		}

		public static string[] GetFiles(string directory, string filter)
		{
			if(!Directory.Exists(directory)) return new string[0];

			try
			{
				return Directory.GetFiles(directory, filter);
			}
			catch
			{
				return new string[0];
			}
		}
	}
}
