// <copyright>
//
// Rewritten in C# by Yury Kiselev, 2010.
//
// Copyright (C) 1996-1997 Id Software, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  
// 
// See the GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
// </copyright>

using OpenTK;
using OpenTK.Graphics.OpenGL;
using Quarp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

// vid.h -- video driver defs

namespace Quarp
{
    /// <summary>
    /// Vid_functions
    /// </summary>
    static class Vid
    {
        public const int VID_CBITS = 6;
        public const int VID_GRADES = (1 << VID_CBITS);
        
        public const int VID_ROW_SIZE = 3;

        static ushort[] _8to16table = new ushort[256]; // d_8to16table[256]
        static uint[] _8to24table = new uint[256]; // d_8to24table[256]
        static byte[] _15to8table = new byte[65536]; // d_15to8table[65536]

        static mode_t[] _Modes;
        static int _ModeNum; // vid_modenum

        static Cvar _glZTrick;// = { "gl_ztrick", "1" };
        static Cvar _Mode;// = { "vid_mode", "0", false };
        // Note that 0 is MODE_WINDOWED
        static Cvar _DefaultMode;// = { "_vid_default_mode", "0", true };
        // Note that 3 is MODE_FULLSCREEN_DEFAULT
        static Cvar _DefaultModeWin;// = { "_vid_default_mode_win", "3", true };
        static Cvar _Wait;// = { "vid_wait", "0" };
        static Cvar _NoPageFlip;// = { "vid_nopageflip", "0", true };
        static Cvar _WaitOverride;// = { "_vid_wait_override", "0", true };
        public static Cvar ScrHudScale;// = { "scr_hudscale", "2", true };
        static Cvar _StretchBy2;// = { "vid_stretch_by_2", "1", true };
        static Cvar _WindowedMouse;// = { "_windowed_mouse", "1", true };

        static bool _Windowed; // windowed
        static bool _IsInitialized; // vid_initialized
        static float _Gamma = 1.0f; // vid_gamma
        static int _DefModeNum;
        static bool _glMTexable = false; // gl_mtexable

        static string _glVendor; // gl_vendor
        static string _glRenderer; // gl_renderer
        static string _glVersion; // gl_version
        static string _glExtensions; // gl_extensions

        public static ushort[] Table8to16
        {
            get { return _8to16table; }
        }
        public static uint[] Table8to24
        {
            get { return _8to24table; }
        }
        public static byte[] Table15to8
        {
            get { return _15to8table; }
        }
        public static bool glMTexable
        {
            get { return _glMTexable; }
        }
        public static bool glZTrick
        {
            get { return (_glZTrick.Value != 0); }
        }
        public static bool WindowedMouse
        {
            get { return _WindowedMouse.Value != 0; }
        }
        public static bool Wait
        {
            get { return _Wait.Value != 0; }
        }
        public static mode_t[] Modes
        {
            get { return _Modes; }
        }
        public static int ModeNum
        {
            get { return _ModeNum; }
        }

        static Vid()
        {
            ScrHudScale = new Cvar("scr_hudscale", "2", true);
        }

        public static bool Initialized { get; private set; } = false;

        // VID_Init (unsigned char *palette)
        // Called at startup to set up translation tables, takes 256 8 bit RGB values
        // the palette data will go away after the call, so it must be copied off if
        // the video driver will need it again
        public static void Init (byte[] palette)
        {
            Initialized = false;

            if (_glZTrick == null)
            {
                #region . define cvars .

                _glZTrick = new Cvar("gl_ztrick", "1");
                _Mode = new Cvar("vid_mode", "0", false);
                _DefaultMode = new Cvar("_vid_default_mode", "0", true);
                _DefaultModeWin = new Cvar("_vid_default_mode_win", "3", true);
                _Wait = new Cvar("vid_wait", "0");
                _NoPageFlip = new Cvar("vid_nopageflip", "0", true);
                _WaitOverride = new Cvar("_vid_wait_override", "0", true);
                _StretchBy2 = new Cvar("vid_stretch_by_2", "1", true);
                _WindowedMouse = new Cvar("_windowed_mouse", "1", true);

                Cmd.Add("vid_nummodes", NumModes_f);
                Cmd.Add("vid_describecurrentmode", DescribeCurrentMode_f);
                Cmd.Add("vid_describemode", DescribeMode_f);
                Cmd.Add("vid_describemodes", DescribeModes_f);
                Cmd.Add("vid_restart", () => Init(palette));

                #endregion
            }


            var display = MainForm.DisplayDevice;

            #region . Enumerate available modes, skip 8 bpp modes, and group by refresh rates .

            var tmp = new List<mode_t>(display.AvailableResolutions.Count);
            foreach (var res in display.AvailableResolutions)
            {
                if (res.BitsPerPixel <= 8)
                    continue;

                if (tmp.Exists(m => m.Is(res)))
                    continue;

                var mode = new mode_t
                {
                    width = res.Width,
                    height = res.Height,
                    bpp = res.BitsPerPixel,
                    refreshRate = res.RefreshRate
                };
                tmp.Add(mode);
            }
            _Modes = tmp.ToArray();

            #endregion

            var mode1 = new mode_t
            {
                width = display.Width,
                height = display.Height,
                bpp = display.BitsPerPixel,
                refreshRate = display.RefreshRate,
                fullScreen = true
            };

            var width = display.Width;
            var height = display.Height;
            var i = Common.CheckParm("-width");
            if (i > 0 && i < Common.Argc - 1)
            {
                width = Common.atoi(Common.Argv(i + 1));

                foreach (var res in display.AvailableResolutions)
                {
                    if (res.Width != width)
                        continue;

                    height = res.Height;
                    break;
                }
            }

            i = Common.CheckParm("-height");
            if (i > 0 && i < Common.Argc - 1)
                height = Common.atoi(Common.Argv(i + 1));

            mode1.width = width;
            mode1.height = height;

	        if (Common.HasParam("-window"))
	        {
		        _Windowed = true;
                MainForm.Instance.Size = new Size(width, height);
	        }
            else
	        {
                _Windowed = false;
                
                if (Common.HasParam("-current"))
                {
                    mode1.width = display.Width;
                    mode1.height = display.Height;
                }
                else
                {
                    var bpp = mode1.bpp;
                    i = Common.CheckParm("-bpp");
                    if (i > 0 && i < Common.Argc - 1)
                    {
                        bpp = Common.atoi(Common.Argv(i + 1));
                    }
                    mode1.bpp = bpp;
                }
	        }

	        _IsInitialized = true;

            Scr.vid.colormap = Host.ColorMap;
            var v = BitConverter.ToInt32(Host.ColorMap, 2048);
            Scr.vid.fullbright = 256 - Common.LittleLong(v);

            CheckGamma(palette);
            SetPalette(palette);

            mode1.fullScreen = !_Windowed;

            _DefModeNum = -1;
            for (i = 0; i < _Modes.Length; ++i)
            {
                var m = _Modes[i];
                if (m.width != mode1.width || m.height != mode1.height)
                    continue;

                _DefModeNum = i;

                if (m.bpp == mode1.bpp && Math.Abs(m.refreshRate - mode1.refreshRate) < 0.1)
                    break;
            }
            if (_DefModeNum == -1)
                _DefModeNum = 0;

            SetMode(_DefModeNum, palette);

            InitOpenGL();

            // TODO remove
            Directory.CreateDirectory(Path.Combine(Common.GameDir, "glquake"));

            Initialized = true;
        }

        public static void SetHudScale()
        {
            Scr.vid.height = HudSystem.Hud.Height;
            Scr.vid.width = HudSystem.Hud.Width;
            Scr.vid.recalc_refdef = true;
        }

        /// <summary>
        /// GL_Init
        /// </summary>
        private static void InitOpenGL()
        {
            _glVendor = GL.GetString(StringName.Vendor);
            Con.Print("GL_VENDOR: {0}\n", _glVendor);
            _glRenderer = GL.GetString(StringName.Renderer);
            Con.Print("GL_RENDERER: {0}\n", _glRenderer);

            _glVersion = GL.GetString(StringName.Version);
            Con.Print("GL_VERSION: {0}\n", _glVersion);
            _glExtensions = GL.GetString(StringName.Extensions);
            Con.Print("GL_EXTENSIONS: {0}\n", _glExtensions);

            if (_glRenderer.StartsWith("PowerVR", StringComparison.InvariantCultureIgnoreCase))
                Scr.FullSbarDraw = true;

            if (_glRenderer.StartsWith("Permedia", StringComparison.InvariantCultureIgnoreCase))
                Scr.IsPermedia = true;

            CheckTextureExtensions();
            CheckMultiTextureExtensions();

            GL.ClearColor(1, 0, 0, 0);
            GL.CullFace(CullFaceMode.Front);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Greater, 0.666f);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.ShadeModel(ShadingModel.Flat);

            Drawer.SetTextureFilters(TextureMinFilter.Nearest, TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Replace);
        }
        
        /// <summary>
        /// VID_Shutdown
        /// Called at shutdown
        /// </summary>
        public static void Shutdown()
        {
            _IsInitialized = false;
        }
        
        // VID_SetMode (int modenum, unsigned char *palette)
        // sets the mode; only used by the Quake engine for resetting to mode 0 (the
        // base mode) on memory allocation failures
        public static void SetMode(int modenum, byte[] palette)
        {
            if (modenum < 0 || modenum >= _Modes.Length)
            {
                Sys.Error("Bad video mode\n");
            }
            
            mode_t mode = _Modes[modenum];
            
            // so Con_Printfs don't mess us up by forcing vid and snd updates
            bool temp = Scr.IsDisabledForLoading;
            Scr.IsDisabledForLoading = true;

            CDAudio.Pause();

            // Set either the fullscreen or windowed mode
            DisplayDevice dev = MainForm.DisplayDevice;
            MainForm form = MainForm.Instance;
            if (_Windowed)
            {
                form.WindowState = WindowState.Normal;
                form.WindowBorder = WindowBorder.Fixed;
                form.Location = new Point((mode.width - form.Width) / 2, (mode.height - form.Height) / 2);
                if (Math.Abs(_WindowedMouse.Value) > 0.1 && Key.Destination == keydest_t.key_game)
                {
                    Input.ActivateMouse();
                    Input.HideMouse();
                }
                else
                {
                    Input.DeactivateMouse();
                    Input.ShowMouse();
                }
            }
            else
            {
                try
                {
                    dev.ChangeResolution(mode.width, mode.height, mode.bpp, mode.refreshRate);
                }
                catch (Exception ex)
                {
                    Sys.Error("Couldn't set video mode: " + ex.Message);
                }
                form.WindowState = WindowState.Fullscreen;
                form.WindowBorder = WindowBorder.Hidden;
            }

            SetHudScale();
            
            Scr.vid.numpages = 2;
            
            CDAudio.Resume();
            Scr.IsDisabledForLoading = temp;

            _ModeNum = modenum;
            Cvar.Set("vid_mode", (float)_ModeNum);

            // fix the leftover Alt from any Alt-Tab or the like that switched us away
            ClearAllStates();

            Con.SafePrint("Video mode {0} initialized.\n", GetModeDescription(_ModeNum));

            SetPalette(palette);
        }

        // VID_NumModes_f
        static void NumModes_f()
        {
            int nummodes = _Modes.Length;
	        if (nummodes == 1)
		        Con.Print("{0} video mode is available\n", nummodes);
	        else
		        Con.Print("{0} video modes are available\n", nummodes);
        }

        // VID_DescribeCurrentMode_f
        static void DescribeCurrentMode_f()
        {
	        Con.Print("{0}\n", GetExtModeDescription(_ModeNum));
        }

                
        // VID_DescribeMode_f
        static void DescribeMode_f()
        {
            int modenum = Common.atoi(Cmd.Argv(1));

            Con.Print("{0}\n", GetExtModeDescription(modenum));
        }


        // VID_DescribeModes_f
        static void DescribeModes_f()
        {
            for (int i = 0; i < _Modes.Length; i++)
            {
                Con.Print("{0}:{1}\n", i, GetExtModeDescription(i));
            }
        }

        
        /// <summary>
        /// VID_GetModeDescription
        /// </summary>
        public static string GetModeDescription(int mode)
        {
	        if (mode < 0 || mode >= _Modes.Length)
		        return string.Empty;

            var m = _Modes[mode];
            var w = _Windowed ? "windowed" : "fullscreen";
            return $"{m.width}x{m.height}x{m.bpp} {w}";
        }

        static string GetExtModeDescription (int mode)
        {
            return GetModeDescription(mode);
        }

        // Check_Gamma
        static void CheckGamma(byte[] pal)
        {
            int i = Common.CheckParm("-gamma");
            if (i == 0)
            {
                string renderer = GL.GetString(StringName.Renderer);
                string vendor = GL.GetString(StringName.Vendor);
                if (renderer.Contains("Voodoo") || vendor.Contains("3Dfx"))
                    _Gamma = 1;
                else
                    _Gamma = 0.7f; // default to 0.7 on non-3dfx hardware
            }
            else
                _Gamma = float.Parse(Common.Argv(i + 1));

            for (i = 0; i < pal.Length; i++)
            {
                double f = Math.Pow((pal[i] + 1) / 256.0, _Gamma);
                double inf = f * 255 + 0.5;
                if (inf < 0)
                    inf = 0;
                if (inf > 255)
                    inf = 255;
                pal[i] = (byte)inf;
            }
        }

        /// <summary>
        /// VID_SetPalette
        /// called at startup and after any gamma correction
        /// </summary>
        public static void SetPalette(byte[] palette)
        {
            //
            // 8 8 8 encoding
            //
            int offset = 0;
	        byte[] pal = palette;
	        uint[] table = _8to24table;
            for (int i = 0; i < table.Length; i++)
            {
                uint r = pal[offset + 0];
                uint g = pal[offset + 1];
                uint b = pal[offset + 2];

                table[i] = ((uint)0xff << 24) + (r << 0) + (g << 8) + (b << 16);
                offset += 3;
            }

            table[255] &= 0xffffff;	// 255 is transparent

            // JACK: 3D distance calcs - k is last closest, l is the distance.
            // FIXME: Precalculate this and cache to disk.
            Union4b val = Union4b.Empty;
            for (uint i = 0; i < (1 << 15); i++)
            {
                // Maps
                // 000000000000000
                // 000000000011111 = Red  = 0x1F
                // 000001111100000 = Blue = 0x03E0
                // 111110000000000 = Grn  = 0x7C00
                uint r = (((i & 0x1F) << 3) + 4);
                uint g = (((i & 0x03E0) >> 2) + 4);
                uint b = (((i & 0x7C00) >> 7) + 4);
                uint k = 0;
                uint l = 10000 * 10000;
                for (uint v = 0; v < 256; v++)
                {
                    val.ui0 = _8to24table[v];
                    uint r1 = r - val.b0;
                    uint g1 = g - val.b1;
                    uint b1 = b - val.b2;
                    uint j = (r1 * r1) + (g1 * g1) + (b1 * b1);
                    if (j < l)
                    {
                        k = v;
                        l = j;
                    }
                }
                _15to8table[i] = (byte)k;
            }
        }

        // ClearAllStates
        static void ClearAllStates()
        {
            // send an up event for each key, to make sure the server clears them all
            for (int i = 0; i < 256; i++)
            {
                Key.Event(i, false);
            }

            Key.ClearStates();
	        Input.ClearStates();
        }

        /// <summary>
        /// CheckTextureExtensions
        /// </summary>
        static void CheckTextureExtensions ()
        {
            const string TEXTURE_EXT_STRING = "GL_EXT_texture_object";

            // check for texture extension
            bool texture_ext = _glExtensions.Contains(TEXTURE_EXT_STRING);
        }

        /// <summary>
        /// CheckMultiTextureExtensions
        /// </summary>
        static void CheckMultiTextureExtensions() 
        {
	        if (_glExtensions.Contains("GL_SGIS_multitexture ") && !Common.HasParam("-nomtex"))
            {
		        Con.Print("Multitexture extensions found.\n");
		        _glMTexable = true;
	        }
        }
    }

    
    struct vrect_t
    {
	    public int x, y, width, height;
    } // vrect_t;


    class viddef_t
    {
        public byte[] colormap;		// 256 * VID_GRADES size
        public int fullbright;		// index of first fullbright color
        public int rowbytes; // unsigned	// may be > width if displayed in a window
        public int width; // unsigned		
        public int height; // unsigned
        public float aspect;		// width / height -- < 0 is taller than wide
        public int numpages;
        public bool recalc_refdef;	// if true, recalc vid-based stuff
    } // viddef_t;

    class mode_t
    {
        public int width;
        public int height;
        public int bpp;
        public float refreshRate;
        public bool fullScreen;
    }
}
