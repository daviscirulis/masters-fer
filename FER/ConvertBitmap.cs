using System;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FER
{
	class ConvertBitmap
	{
		[System.Runtime.InteropServices.DllImport("gdi32.dll")]

		public static extern bool DeleteObject(IntPtr handle);
        public static BitmapSource bitmapSource;
		public static IntPtr intPointer;
		public static BitmapSource BitmapToBitmapSource(Bitmap bitmap)

		{
			intPointer = bitmap.GetHbitmap();
			bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(intPointer,IntPtr.Zero,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
			DeleteObject(intPointer);
			return bitmapSource;
		}

        public static BitmapSource Convert(Bitmap bitmap, PixelFormat format)
        {

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height, 96, 96, format, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        public static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }
}
