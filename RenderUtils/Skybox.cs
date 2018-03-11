using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Quarp.Extensions;

namespace Quarp.RenderUtils
{
    public static class Skybox
    {

        private static string _skyName;

        private static Vector3 _skyRotation = new Vector3(0, 0, 0);

        private static string _skyRotationString = "0 0 0";

        private sealed class SkySideInfo
        {
            public string Trail;

            public int TextureIndex;

            public Vector3[] Verteces;

            public byte[] TexCoords;
        }

        private static readonly SkySideInfo[] SkyInfo =
        {
            new SkySideInfo
            {
                Trail = "up",
                TexCoords = new byte[] {0, 0, 1, 0, 1, 1, 0, 1},
                Verteces = new []
                {
                    new Vector3(-SkySize, -SkySize, +SkySize),
                    new Vector3(+SkySize, -SkySize, +SkySize),
                    new Vector3(+SkySize, +SkySize, +SkySize),
                    new Vector3(-SkySize, +SkySize, +SkySize)
                }
            },
            new SkySideInfo
            {
                Trail = "dn",
                TexCoords = new byte[] {0, 1, 0, 0, 1, 0, 1, 1},
                Verteces = new []
                {
                    new Vector3(-SkySize, -SkySize, -SkySize),
                    new Vector3(-SkySize, +SkySize, -SkySize),
                    new Vector3(+SkySize, +SkySize, -SkySize),
                    new Vector3(+SkySize, -SkySize, -SkySize)
                }
            },
            new SkySideInfo
            {
                Trail = "rt",
                TexCoords = new byte[] {0, 1, 0, 0, 1, 0, 1, 1},
                Verteces = new []
                {
                    new Vector3(-SkySize, +SkySize, -SkySize),
                    new Vector3(-SkySize, +SkySize, +SkySize),
                    new Vector3(+SkySize, +SkySize, +SkySize),
                    new Vector3(+SkySize, +SkySize, -SkySize)
                }
            },
            new SkySideInfo
            {
                Trail = "lf",
                TexCoords = new byte[] {1, 1, 0, 1, 0, 0, 1, 0},
                Verteces = new []
                {
                    new Vector3(-SkySize, -SkySize, -SkySize),
                    new Vector3(+SkySize, -SkySize, -SkySize),
                    new Vector3(+SkySize, -SkySize, +SkySize),
                    new Vector3(-SkySize, -SkySize, +SkySize)
                }
            },
            new SkySideInfo
            {
                Trail = "ft",
                TexCoords = new byte[] {1, 1, 0, 1, 0, 0, 1, 0},
                Verteces = new []
                {
                    new Vector3(+SkySize, -SkySize, -SkySize),
                    new Vector3(+SkySize, +SkySize, -SkySize),
                    new Vector3(+SkySize, +SkySize, +SkySize),
                    new Vector3(+SkySize, -SkySize, +SkySize)
                }
            },
            new SkySideInfo
            {
                Trail = "bk",
                TexCoords = new byte[] {0, 0, 1, 0, 1, 1, 0, 1},
                Verteces = new []
                {
                    new Vector3(-SkySize, -SkySize, +SkySize),
                    new Vector3(-SkySize, +SkySize, +SkySize),
                    new Vector3(-SkySize, +SkySize, -SkySize),
                    new Vector3(-SkySize, -SkySize, -SkySize)
                }
            }
        };

        public const int SkySize = 8192;

        private static void LoadSkyBoxTextures()
        {
            var loaded = false;
            foreach (var side in SkyInfo)
            {
                var path = Image.Loader.FindTexture($"env\\{Render.Sky.String}_{side.Trail}");
                if (string.IsNullOrEmpty(path))
                    path = Image.Loader.FindTexture($"sky\\{Render.Sky.String}_{side.Trail}");

                if (string.IsNullOrEmpty(path))
                    continue;

                var data = Image.Loader.Load(path, out var h, out var w);
                side.TextureIndex = Drawer.LoadExternalTexture($"{Render.Sky.String}_{side.Trail}", data, w, h, false, false);
                loaded = true;
            }
            if (loaded)
                _skyName = Render.Sky.String;
            else
                Render.Sky.Set(_skyName);
        }

        private static void SetRotationVector()
        {
            if (string.IsNullOrEmpty(Render.SkyRotation.String))
            {
                Render.SkyRotation.Set("0 0 0");
                if (_skyRotationString == Render.SkyRotation.String)
                    return;
            }

            try
            {
                var split = Render.SkyRotation.String.Split(new[] {' '}, 3);
                var v = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
                _skyRotation = v;
                _skyRotationString = Render.SkyRotation.String;
            }
            catch (Exception e)
            {
                Con.Print($"Can't interpret r_skyrotation {Render.SkyRotation.String} : {e.Message}\n");
                Render.SkyRotation.Set("0 0 0");
            }
        }

        public static bool DrawSkyBox()
        {

            if (_skyName != Render.Sky.String)
            {
                LoadSkyBoxTextures();
            }
            if (string.IsNullOrEmpty(_skyName))
                return false;
            if (_skyRotationString != Render.SkyRotation.String)
            {
                SetRotationVector();
            }
            GL.PushMatrix();

            GL.Translate(Render.Origin);

            GL.Rotate(_skyRotation.Y * Host.RealTime, 0, 0, 1);
            GL.Rotate(-_skyRotation.X * Host.RealTime, 0, 1, 0);
            GL.Rotate(_skyRotation.Z * Host.RealTime, 1, 0, 0);


            Render.DisableMultitexture();

            foreach (var side in SkyInfo)
            {
                Drawer.Bind(side.TextureIndex);
                GL.Begin(PrimitiveType.Polygon);
                var i = -1;
                foreach (var vertex in side.Verteces)
                {
                    GL.TexCoord2(side.TexCoords[++i], side.TexCoords[++i]);
                    GL.Vertex3(Render.Origin + vertex);
                }

                GL.End();
            }

            Render.EnableMultitexture();

            GL.PopMatrix();

            return true;
        }
    }
}
