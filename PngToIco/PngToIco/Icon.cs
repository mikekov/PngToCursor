using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PngToIco
{
	public class Icon
	{
		public IList<SingleImage> Images { get; } = new List<SingleImage>();

		public void Save(Stream output, bool store_bmp, bool cursor)
		{
			if (output == null)
				throw new ArgumentNullException();

			if (Images.Any(bmp => bmp.Image.PixelWidth > MAX_SIZE || bmp.Image.PixelHeight > MAX_SIZE))
				throw new InvalidDataException("Images too big");

			if (!Images.Any())
				throw new InvalidDataException("No images");

			if (Images.Count > 0xffff)
				throw new InvalidDataException("Too many images");

			// encode bitmaps
			var bitmaps = Images.Select(bmp => SaveBitmap(bmp.Image, bmp.Mask, store_bmp)).ToArray();

			// create ICO header

			// reserved - 0
			output.WriteByte(0);
			output.WriteByte(0);
			// type - 1 (ICO), 2 (CUR)
			output.WriteByte((byte)(cursor ? 2 : 1));
			output.WriteByte(0);
			// number of images
			output.WriteByte((byte)(Images.Count & 0xff));
			output.WriteByte((byte)((Images.Count >> 8) & 0xff));

			int offset = 6; // 6 bytes header written
			int entry_size = 16; // 16 bytes per entry

			// offset to the first image past all entries
			offset += Images.Count * entry_size;

			/*
			Image entry
			ICONDIRENTRY structure Offset# 	Size (in bytes) 	Purpose
			0 	1 	Specifies image width in pixels. Can be any number between 0 and 255. Value 0 means image width is 256 pixels.
			1 	1 	Specifies image height in pixels. Can be any number between 0 and 255. Value 0 means image height is 256 pixels.
			2 	1 	Specifies number of colors in the color palette. Should be 0 if the image does not use a color palette.
			3 	1 	Reserved. Should be 0.[Notes 2]
			4 	2 	In ICO format: Specifies color planes. Should be 0 or 1.[Notes 3]
					In CUR format: Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
			6 	2 	In ICO format: Specifies bits per pixel. [Notes 4]
					In CUR format: Specifies the vertical coordinates of the hotspot in number of pixels from the top.
			8 	4 	Specifies the size of the image's data in bytes
			12 	4 	Specifies the offset of BMP or PNG data from the beginning of the ICO/CUR file
			*/
			// create directory entries
			for (int i = 0; i < Images.Count; ++i)
			{
				var single_image = Images[i];
				var image = single_image.Image;
				var mask = single_image.Mask;
				// 0. byte
				output.WriteByte((byte)image.PixelWidth);
				// 1. byte
				output.WriteByte((byte)(image.PixelHeight));
				// 2. byte: 0 - no color palette
				output.WriteByte(0);
				// 3. byte: reserved
				output.WriteByte(0);
				// 4. short * 2
				if (cursor)
				{
					// hot spot X, Y
					output.WriteByte(single_image.HotSpotX);
					output.WriteByte(0);
					output.WriteByte(single_image.HotSpotY);
					output.WriteByte(0);
				}
				else
				{
					// color planes
					output.WriteByte(1);
					output.WriteByte(0);
					// bits per pixel
					output.WriteByte(0);
					output.WriteByte(0);
				}
				// image size
				var bmp = bitmaps[i];
				var size = cursor ? bmp.Item1.Length + bmp.Item2.Length : bmp.Item1.Length;
				WriteInt(output, size);
				// offset to image
				WriteInt(output, offset);

				offset += size;
			}

			// output images
			foreach (var bmp in bitmaps)
			{
				var image = bmp.Item1;
				var mask = bmp.Item2;
				output.Write(image, 0, image.Length);
				if (cursor)
					output.Write(mask, 0, mask.Length);
			}
		}

		void WriteInt(Stream output, int value)
		{
			output.WriteByte((byte)(value & 0xff));
			output.WriteByte((byte)((value >> 8) & 0xff));
			output.WriteByte((byte)((value >> 16) & 0xff));
			output.WriteByte((byte)((value >> 24) & 0xff));
		}

		const int MAX_SIZE = 256;

		/*
		Offset (hex) 	Offset (dec) 	Size (bytes) 	Windows BITMAPINFOHEADER[1]
		0E 	14 	4 	the size of this header (40 bytes)
		12 	18 	4 	the bitmap width in pixels (signed integer)
		16 	22 	4 	the bitmap height in pixels (signed integer)
		1A 	26 	2 	the number of color planes (must be 1)
		1C 	28 	2 	the number of bits per pixel, which is the color depth of the image. Typical values are 1, 4, 8, 16, 24 and 32.
		1E 	30 	4 	the compression method being used. See the next table for a list of possible values
		22 	34 	4 	the image size. This is the size of the raw bitmap data; a dummy 0 can be given for BI_RGB bitmaps.
		26 	38 	4 	the horizontal resolution of the image. (pixel per meter, signed integer)
		2A 	42 	4 	the vertical resolution of the image. (pixel per meter, signed integer)
		2E 	46 	4 	the number of colors in the color palette, or 0 to default to 2n
		32 	50 	4 	the number of important colors used, or 0 when every color is important; generally ignored
		*/

		byte[] SaveAsBitmap(BitmapSource bmp, bool strip_header)
		{
			var encoder = new BmpBitmapEncoder();
			using (var stream = new MemoryStream())
			{
				encoder.Frames.Add(BitmapFrame.Create(bmp));
				encoder.Save(stream);
				var bitmap = stream.ToArray();
				// skip bitmap file header
				int len = 14;
				if (strip_header)
					len += 40;  // sizeof BITMAPINFOHEADER
				if (bitmap.Length <= len)
					throw new Exception("Bitmap encoding error. Resulting buffer is too small.");
				var copy = new byte[bitmap.Length - len];
				Array.Copy(bitmap, len, copy, 0, copy.Length);
				return copy;
			}
		}

		Tuple<byte[], byte[]> SaveBitmap(BitmapSource bmp, byte[] mask, bool store_bmp)
		{
			byte[] image;
			if (store_bmp)
			{
				image = SaveAsBitmap(bmp, false);
				// tweak height
				//
				int i = 8; // offset to height
				// it needs to be twice the height of bitmap
				var h = bmp.PixelHeight * 2;
				image[i++] = (byte)(h & 0xff);
				image[i++] = (byte)((h >> 8) & 0xff);
				image[i++] = (byte)((h >> 16) & 0xff);
				image[i++] = (byte)((h >> 24) & 0xff);
			}
			else
			{
				var encoder = new PngBitmapEncoder();
				using (var stream = new MemoryStream())
				{
					encoder.Frames.Add(BitmapFrame.Create(bmp));
					encoder.Save(stream);
					image = stream.ToArray();
				}
			}

			return Tuple.Create(image, mask);
		}
	}

	public class SingleImage
	{
		public SingleImage(BitmapSource image, byte[] mask, byte x, byte y)
		{
			Image = image;
			Mask = mask;
			HotSpotX = x;
			HotSpotY = y;
		}

		public BitmapSource Image { get; }
		public byte[] Mask { get; }

		// cursor only
		public byte HotSpotX { get; }
		public byte HotSpotY { get; }
	}
}
