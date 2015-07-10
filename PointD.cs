using System;

namespace Free.Controls.OpenStreetMap
{
	public struct PointD
	{
		public double X;
		public double Y;

		public PointD(double x, double y)
		{
			X=x;
			Y=y;
		}

		public override string ToString()
		{
			return String.Format("({0}, {1})", X, Y);
		}
	}
}
