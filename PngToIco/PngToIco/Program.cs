using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PngToIco
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Required: input image file, output file name, hot spot list.");
				return;
			}

			try
			{
				var input = args[0];
				var output = args[1];
				var hot_spots = args[2];

				var d = new PngBitmapDecoder(new Uri(input), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
				var bmp = d.Frames.First();
				var dim = new List<int>(new int[] { 128, 96, 64, 48, 32 });
				var xys = hot_spots.Split(',').Select(s => double.Parse(s, NumberFormatInfo.InvariantInfo));
				var spots = xys.Zip(xys.Skip(1), (x, y) => new Point(x, y));

				var icon = Slicer.SliceBitmap(bmp, dim, spots);
				bool cursor = true;  // save as cursor, not icon
				bool store_bmp = true;	// store BMPs rather than PNGs inside cursor

				using (var outputf = new FileStream(output, FileMode.Create))
					icon.Save(outputf, store_bmp, cursor);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
			}
		}
	}
}
