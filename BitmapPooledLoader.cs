using System.Collections.Generic;
using System.Drawing;

namespace Free.Controls.OpenStreetMap
{
	internal class BitmapPooledLoader
	{
		Dictionary<string, Bitmap> imgPool;
		List<string> imgLoadOrder;

		public BitmapPooledLoader()
		{
			MaxImages=20;
			imgPool=new Dictionary<string, Bitmap>();
			imgLoadOrder=new List<string>();
		}

		public BitmapPooledLoader(uint mxi)
		{
			MaxImages=mxi;
			imgPool=new Dictionary<string, Bitmap>();
			imgLoadOrder=new List<string>();
		}

		public Bitmap FromFile(string filename)
		{
			string lower=filename.ToLower();

			if(imgPool.ContainsKey(lower))
			{
				imgLoadOrder.Remove(lower);
				imgLoadOrder.Add(lower);
				return imgPool[lower];
			}

			while(MaxImages<=imgPool.Count)
			{
				string del=imgLoadOrder[0];
				imgLoadOrder.RemoveAt(0);
				imgPool.Remove(del);
			}

			try
			{
				Image tmp=Bitmap.FromFile(lower);
				Bitmap ret=new Bitmap(tmp);
				tmp.Dispose();
				tmp=null;

				imgPool.Add(lower, ret);
				imgLoadOrder.Add(lower);

				return ret;
			}
			catch { return null; }
		}

		public void Clear()
		{
			imgPool.Clear();
			imgLoadOrder.Clear();
		}

		public uint MaxImages { get; set; }

		public int Count
		{
			get { return imgLoadOrder.Count; }
		}

		public bool RemoveFileFromPool(string filename)
		{
			string lower=filename.ToLower();

			if(imgPool.ContainsKey(lower))
			{
				imgLoadOrder.Remove(lower);
				imgPool.Remove(lower);
				return true;
			}

			return false;
		}
	}
}
