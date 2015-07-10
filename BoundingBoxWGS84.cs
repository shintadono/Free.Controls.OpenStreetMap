using System;

namespace Free.Controls.OpenStreetMap
{
	// Doesn't work over polarcaps
	public class BoundingBoxWGS84
	{
		public double North, South, East, West;

		public BoundingBoxWGS84(double west, double north, double east, double south):
			this(west, north, east, south, 90) { }

		public BoundingBoxWGS84(double west, double north, double east, double south, double MaxY)
		{
			// normalizing
			North=Math.Max(north, south); if(North>MaxY) North=MaxY;
			South=Math.Min(north, south); if(South<-MaxY) South=-MaxY;

			// when number to big
			if(west>9999999999) { west=90; east=0; }

			while(west<=-180) { west+=360.0; east+=360.0; }
			while(west>180) { west-=360.0; east-=360.0; }

			if(east<=180&&east>-180)
			{ // east not over day border
				West=Math.Min(west, east);
				East=Math.Max(west, east);
			}
			else
			{ // east over day border
				if(east<=west-360||east>=west+360) // one whole earth Box
				{
					West=0;
					East=360;
				}
				else
				{
					while(east<=-180) east+=360;
					while(east>180) east-=360;

					East=Math.Min(west, east)+360;
					West=Math.Max(west, east);
				}
			}
		}

		public override string ToString()
		{
			return string.Format("{0:F5}° {1:F5}° {2:F5}° {3:F5}°", West, North, East, South);
		}

		/// <summary>
		/// Width (orthodrome) of the box in radians. [0, 2*Math.PI]
		/// </summary>
		public double Width
		{
			get
			{
				double phi=(North+South)*Math.PI/360; // mean of the phis' in radians ((N+S)/2)*D2R
				double lam=(East-West)*Math.PI/180; // diff of the lamdas' in radians (E-W)*D2R

				double ret=2*Math.Asin(Math.Cos(phi)*Math.Sin(lam/2)); // (-Math.PI, Math.PI]

				if(lam>=Math.PI) return Math.PI*2-ret; // more than 180 degree, the other way arong

				return ret;
			}
		}

		/// <summary>
		/// Height of the box in radians. [0, Math.PI]
		/// </summary>
		public double Height
		{
			get
			{
				return (North-South)*Math.PI/180;
			}
		}
	}
}
