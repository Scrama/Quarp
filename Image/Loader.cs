using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Quarp.Image
{
    public static class Loader
    {
        public static byte[] Load(string path, out int height, out int width)
        {
            var ext = Path.GetExtension(path)?.ToLower().TrimStart('.');
            byte[] result;
            if (new[] {"jpg", "jpeg", "png"}.Contains(ext))
                result = LoadJpg(path, out height, out width);
            else if (ext == "tga")
                result = LoadTga(path, out height, out width);
            else
                throw new Exception($"{path} has bad format");

            if (width * height * 3 == result.Length)
                return ExtendBpp(result);
            if (width * height * 4 == result.Length)
                return result;

            throw new Exception($"{path} has bad bpp");
        }

        private static byte[] LoadTga(string path, out int height, out int width)
        {
            using (var tga = new TargaImage(path))
            {
                height = tga.Header.Height;
                width = tga.Header.Width;
                return tga.ImageData.ToArray();
            }
        }

        private static byte[] LoadJpg(string path, out int height, out int width)
        {
            using (var jpg = System.Drawing.Image.FromFile(path))
            {
                using (var ms = new MemoryStream())
                {
                    jpg.Save(ms, ImageFormat.Bmp);

                    height = jpg.Height;
                    width = jpg.Width;

                    var buffer = new byte[4];
                    ms.Seek(10, SeekOrigin.Begin);
                    ms.Read(buffer, 0, buffer.Length);
                    var ofs = BitConverter.ToInt32(buffer, 0);

                    buffer = new byte[ms.Length - ofs];
                    ms.Seek(ofs, SeekOrigin.Begin);
                    ms.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
        }

        private static byte[] ExtendBpp(byte[] bitmap)
        {
            var result = new byte[bitmap.Length + bitmap.Length/3];

            var j = 0;
            for (var i = 0; i < result.Length; i += 4, j += 3)
            {
                result[i] = bitmap[j];
                result[i+1] = bitmap[j+1];
                result[i+2] = bitmap[j+2];
                result[i+3] = 1;
            }

            return result;
        }
    }
}
