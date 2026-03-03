using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace RemoteSystemWpf.Classes
{
    public class ScreenCapture
    {
        public static byte[] CaptureScreen()
        {
            try
            {
               Rectangle bounds = Screen.PrimaryScreen.Bounds;

                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                       EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 70L);

                        ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                        if (jpegCodec != null)
                        {
                            bitmap.Save(ms, jpegCodec, encoderParams);
                        }
                        else
                        {
                            bitmap.Save(ms, ImageFormat.Jpeg);
                        }

                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка захвата экрана: " + ex.Message);
                return null;
            }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            return null;
        }
    }
}