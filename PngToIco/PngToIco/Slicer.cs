using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PngToIco
{
	static class Slicer
	{
		public static Icon SliceBitmap(BitmapSource bmp, IEnumerable<int> dimensions, IEnumerable<Point> hot_spots)
		{
			var icon = new Icon();
			int offset = 0;
			var spot = hot_spots.GetEnumerator();
			Point last_spot = new Point(0, 0);
			int last_size = 1;

			foreach (var dim in dimensions)
			{
				byte x = 0, y = 0;
				if (spot.MoveNext())
				{
					last_spot = spot.Current;
					last_size = dim;
					x = (byte)spot.Current.X;
					y = (byte)spot.Current.Y;
				}
				else
				{
					// scale down last hot spot
					x = (byte)(last_spot.X * dim / last_size);
					y = (byte)(last_spot.Y * dim / last_size);
				}

				// icon/cursor image proper:
				var src_rect = new Int32Rect(offset, 0, dim, dim);
				var image = new CroppedBitmap(bmp, src_rect);

				// TODO: ths is incorrect
				//var mask = new FormatConvertedBitmap(image, PixelFormats.BlackWhite, null, 0.1);

				var mask = CreateMask(image, dim);

				icon.Images.Add(new SingleImage(image, mask, x, y));
				offset += dim;
			}

			return icon;
		}

		static byte[] CreateMask(BitmapSource image, int dim)
		{
			if ((dim & 7) != 0)
				throw new ArgumentException("bad size, not divisible by 8");

			// assuming RGBA
			var pixels = new uint[dim * dim];

			// mask: black&white image, 8 pixels per byte
			int mask_stride = (((dim + 7) / 8) + 3) & ~3;
			var mask = new byte[mask_stride * dim];

			image.CopyPixels(pixels, dim * 4, 0);

			for (int y = 0; y < dim; ++y)
				for (int x = 0; x < dim; x += 8)
				{
					byte pix = 0;
					for (int i = 0; i < 8; ++i)
					{
						var p = pixels[i + x + y * dim] >> 24;
						if (p > 127)
							pix |= (byte)(1 << i);
					}
					mask[x / 8 + y * mask_stride] = pix;
				}

			return mask;
		}
	}
}
