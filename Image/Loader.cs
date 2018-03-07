using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

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
                result = LoadJpg(path, out height, out width);
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
