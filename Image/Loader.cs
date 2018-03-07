using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Quarp.Image
{
    public static class Loader
    {
        private static readonly string[] Patterns =
        {
            "{0}\\textures\\{1}.tga",
            "{0}\\textures\\{1}.png",
            "{0}\\textures\\{1}.jpg",
            "{0}\\textures\\{1}.jpeg"
        };

        public static string FindTexture(string name)
        {
            name = name.Replace('*', '#');

            foreach (var pattern in Patterns)
            {
                var path = string.Format(pattern, Common.GameDir, name);
                if (File.Exists(path))
                    return path;
                if (!Common.IsModified)
                    continue;
                // search in base folder
                path = string.Format(pattern, QDef.GAMENAME, name);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        public static uint[] Load(string path, out int height, out int width)
        {
            var ext = Path.GetExtension(path)?.ToLower().TrimStart('.');
            byte[] result;
            if (new[] {"jpg", "jpeg", "png"}.Contains(ext))
                result = LoadMime(path, out height, out width);
            else if (ext == "tga")
                result = LoadTga(path, out height, out width);
            else
                throw new Exception($"{path} has bad format");

            if (width * height * 3 == result.Length)
                return ExtendBpp(result, width * height);
            if (width * height * 4 == result.Length)
                return ToUint(result);

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

        private static byte[] LoadMime(string path, out int height, out int width)
        {
            using (var img = new Bitmap(path))
            {
                width = img.Width;
                height = img.Height;
                var result = new byte[width*height*4];
                var bmp = img.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bmp.Scan0, result, 0, result.Length);
                return result;
            }
        }

        private static uint[] ExtendBpp(byte[] bitmap, int size)
        {
            var result = new uint[size];

            var buffer = new byte[] {0, 0, 0, byte.MaxValue};

            var j = 0;
            for (var i = 0; i < result.Length; ++i, j += 3)
            {
                buffer[0] = bitmap[j];
                buffer[1] = bitmap[j+1];
                buffer[2] = bitmap[j+2];
                result[i] = BitConverter.ToUInt32(buffer, 0);
            }

            return result;
        }

        private static uint[] ToUint(byte[] bitmap)
        {
            var result = new uint[bitmap.Length/4];

            var j = 0;
            for (var i = 0; i < result.Length; ++i, j += 4)
            {
                result[i] = BitConverter.ToUInt32(bitmap, j);
            }

            return result;
        }
    }
}
