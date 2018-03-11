using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Quarp.HudSystem
{
    internal static class Hud
    {
        public static int Width => _hudEntry?.Width ?? 640;
        public static int Height => _hudEntry?.Height ?? 480;

        static bool _showScores; // sb_showscores

        public static Cvar HudConfig; // = { "hud_config", "default" };

        private const int StatMinus = 10;  // num frame for '-' stats digit

        static readonly glpic_t[,] _nums = new glpic_t[2, 11];
        static glpic_t _colon;
        static glpic_t _slash;
        static glpic_t _iBar;
        static glpic_t _sBar;
        static glpic_t _scoreBar;

        public static readonly glpic_t[,] Weapons = new glpic_t[7, 8];   // 0 is active, 1 is owned, 2-5 are flashes
        public static readonly glpic_t[] Ammo = new glpic_t[4];
        static readonly glpic_t[] _sigil = new glpic_t[4];
        public static readonly glpic_t[] Armor = new glpic_t[3];
        static readonly glpic_t[] _items = new glpic_t[32];

        public static int[] AmmoConsts = { QItems.IT_SHELLS , QItems.IT_NAILS , QItems.IT_ROCKETS , QItems.IT_CELLS };

        public static readonly glpic_t[,] Faces = new glpic_t[7, 2];       // 0 is gibbed, 1 is dead, 2-6 are alive
                                                            // 0 is static, 1 is temporary animation
        public static glpic_t FaceInvis;
        public static glpic_t FaceQuad;
        public static glpic_t FaceInvuln;
        public static glpic_t FaceInvisInvuln;

        private static readonly glpic_t[] _rInvBar = new glpic_t[2];
        static readonly glpic_t[] _rWeapons = new glpic_t[5];
        static readonly glpic_t[] _rItems = new glpic_t[2];
        static readonly glpic_t[] _rAmmo = new glpic_t[3];
        static glpic_t _rTeamBord;		// PGM 01/19/97 - team color border

        //MED 01/04/97 added two more weapons + 3 alternates for grenade launcher
        static readonly glpic_t[,] _hWeapons = new glpic_t[7, 5];   // 0 is active, 1 is owned, 2-5 are flashes
        //MED 01/04/97 added array to simplify weapon parsing
        static int[] _hipWeapons = new int[]
        {
            QItems.HIT_LASER_CANNON_BIT, QItems.HIT_MJOLNIR_BIT, 4, QItems.HIT_PROXIMITY_GUN_BIT
        };
        //MED 01/04/97 added hipnotic items array
        static readonly glpic_t[] _hItems = new glpic_t[2];

        static int[] _fragSort = new int[QDef.MAX_SCOREBOARD];
        static string[] _scoreBoardText = new string[QDef.MAX_SCOREBOARD];
        static int[] _scoreBoardTop = new int[QDef.MAX_SCOREBOARD];
        static int[] _scoreBoardBottom = new int[QDef.MAX_SCOREBOARD];
        static int[] _scoreBoardCount = new int[QDef.MAX_SCOREBOARD];
        static int _scoreBoardLines;

        public static int Lines { get; set; } // sb_lines scan lines to draw


        // Sbar_Init
        public static void Init()
        {
            #region numbers

            for (var i = 0; i < 10; ++i)
            {
                var str = i.ToString();
                _nums[0, i] = Drawer.PicFromWad($"num_{str}");
                _nums[1, i] = Drawer.PicFromWad($"anum_{str}");
            }

            _nums[0, 10] = Drawer.PicFromWad("num_minus");
            _nums[1, 10] = Drawer.PicFromWad("anum_minus");

            _colon = Drawer.PicFromWad("num_colon");
            _slash = Drawer.PicFromWad("num_slash");

            #endregion

            #region weapons

            Weapons[0, 0] = Drawer.PicFromWad("inv_shotgun");
            Weapons[0, 1] = Drawer.PicFromWad("inv_sshotgun");
            Weapons[0, 2] = Drawer.PicFromWad("inv_nailgun");
            Weapons[0, 3] = Drawer.PicFromWad("inv_snailgun");
            Weapons[0, 4] = Drawer.PicFromWad("inv_rlaunch");
            Weapons[0, 5] = Drawer.PicFromWad("inv_srlaunch");
            Weapons[0, 6] = Drawer.PicFromWad("inv_lightng");

            Weapons[1, 0] = Drawer.PicFromWad("inv2_shotgun");
            Weapons[1, 1] = Drawer.PicFromWad("inv2_sshotgun");
            Weapons[1, 2] = Drawer.PicFromWad("inv2_nailgun");
            Weapons[1, 3] = Drawer.PicFromWad("inv2_snailgun");
            Weapons[1, 4] = Drawer.PicFromWad("inv2_rlaunch");
            Weapons[1, 5] = Drawer.PicFromWad("inv2_srlaunch");
            Weapons[1, 6] = Drawer.PicFromWad("inv2_lightng");

            for (var i = 0; i < 5; ++i)
            {
                var s = "inva" + (i + 1);
                Weapons[2 + i, 0] = Drawer.PicFromWad($"{s}_shotgun");
                Weapons[2 + i, 1] = Drawer.PicFromWad($"{s}_sshotgun");
                Weapons[2 + i, 2] = Drawer.PicFromWad($"{s}_nailgun");
                Weapons[2 + i, 3] = Drawer.PicFromWad($"{s}_snailgun");
                Weapons[2 + i, 4] = Drawer.PicFromWad($"{s}_rlaunch");
                Weapons[2 + i, 5] = Drawer.PicFromWad($"{s}_srlaunch");
                Weapons[2 + i, 6] = Drawer.PicFromWad($"{s}_lightng");
            }

            Ammo[0] = Drawer.PicFromWad("sb_shells");
            Ammo[1] = Drawer.PicFromWad("sb_nails");
            Ammo[2] = Drawer.PicFromWad("sb_rocket");
            Ammo[3] = Drawer.PicFromWad("sb_cells");

            #endregion

            #region armor

            Armor[0] = Drawer.PicFromWad("sb_armor1");
            Armor[1] = Drawer.PicFromWad("sb_armor2");
            Armor[2] = Drawer.PicFromWad("sb_armor3");

            #endregion

            #region items

            _items[0] = Drawer.PicFromWad("sb_key1");
            _items[1] = Drawer.PicFromWad("sb_key2");
            _items[2] = Drawer.PicFromWad("sb_invis");
            _items[3] = Drawer.PicFromWad("sb_invuln");
            _items[4] = Drawer.PicFromWad("sb_suit");
            _items[5] = Drawer.PicFromWad("sb_quad");

            _sigil[0] = Drawer.PicFromWad("sb_sigil1");
            _sigil[1] = Drawer.PicFromWad("sb_sigil2");
            _sigil[2] = Drawer.PicFromWad("sb_sigil3");
            _sigil[3] = Drawer.PicFromWad("sb_sigil4");

            #endregion

            #region face

            Faces[4, 0] = Drawer.PicFromWad("face1");
            Faces[4, 1] = Drawer.PicFromWad("face_p1");
            Faces[3, 0] = Drawer.PicFromWad("face2");
            Faces[3, 1] = Drawer.PicFromWad("face_p2");
            Faces[2, 0] = Drawer.PicFromWad("face3");
            Faces[2, 1] = Drawer.PicFromWad("face_p3");
            Faces[1, 0] = Drawer.PicFromWad("face4");
            Faces[1, 1] = Drawer.PicFromWad("face_p4");
            Faces[0, 0] = Drawer.PicFromWad("face5");
            Faces[0, 1] = Drawer.PicFromWad("face_p5");

            FaceInvis = Drawer.PicFromWad("face_invis");
            FaceInvuln = Drawer.PicFromWad("face_invul2");
            FaceInvisInvuln = Drawer.PicFromWad("face_inv2");
            FaceQuad = Drawer.PicFromWad("face_quad");

            #endregion

            Cmd.Add("+showscores", ShowScores);
            Cmd.Add("-showscores", DontShowScores);

            _scoreBar = Drawer.PicFromWad("scorebar");

            #region hipweapons

            //MED 01/04/97 added new hipnotic weapons
            if (Common.GameKind == GameKind.Hipnotic)
            {
                _hWeapons[0, 0] = Drawer.PicFromWad("inv_laser");
                _hWeapons[0, 1] = Drawer.PicFromWad("inv_mjolnir");
                _hWeapons[0, 2] = Drawer.PicFromWad("inv_gren_prox");
                _hWeapons[0, 3] = Drawer.PicFromWad("inv_prox_gren");
                _hWeapons[0, 4] = Drawer.PicFromWad("inv_prox");

                _hWeapons[1, 0] = Drawer.PicFromWad("inv2_laser");
                _hWeapons[1, 1] = Drawer.PicFromWad("inv2_mjolnir");
                _hWeapons[1, 2] = Drawer.PicFromWad("inv2_gren_prox");
                _hWeapons[1, 3] = Drawer.PicFromWad("inv2_prox_gren");
                _hWeapons[1, 4] = Drawer.PicFromWad("inv2_prox");

                for (var i = 0; i < 5; ++i)
                {
                    var s = "inva" + (i + 1);
                    _hWeapons[2 + i, 0] = Drawer.PicFromWad($"{s}_laser");
                    _hWeapons[2 + i, 1] = Drawer.PicFromWad($"{s}_mjolnir");
                    _hWeapons[2 + i, 2] = Drawer.PicFromWad($"{s}_gren_prox");
                    _hWeapons[2 + i, 3] = Drawer.PicFromWad($"{s}_prox_gren");
                    _hWeapons[2 + i, 4] = Drawer.PicFromWad($"{s}_prox");
                }

                _hItems[0] = Drawer.PicFromWad("sb_wsuit");
                _hItems[1] = Drawer.PicFromWad("sb_eshld");
            }

            #endregion

            #region rogue weapons

            if (Common.GameKind == GameKind.Rogue)
            {
                _rInvBar[0] = Drawer.PicFromWad("r_invbar1");
                _rInvBar[1] = Drawer.PicFromWad("r_invbar2");

                _rWeapons[0] = Drawer.PicFromWad("r_lava");
                _rWeapons[1] = Drawer.PicFromWad("r_superlava");
                _rWeapons[2] = Drawer.PicFromWad("r_gren");
                _rWeapons[3] = Drawer.PicFromWad("r_multirock");
                _rWeapons[4] = Drawer.PicFromWad("r_plasma");

                _rItems[0] = Drawer.PicFromWad("r_shield1");
                _rItems[1] = Drawer.PicFromWad("r_agrav1");

                // PGM 01/19/97 - team color border
                _rTeamBord = Drawer.PicFromWad("r_teambord");
                // PGM 01/19/97 - team color border

                _rAmmo[0] = Drawer.PicFromWad("r_ammolava");
                _rAmmo[1] = Drawer.PicFromWad("r_ammomulti");
                _rAmmo[2] = Drawer.PicFromWad("r_ammoplasma");
            }

            #endregion

            HudConfig = new Cvar("hud_config", "default", true);
            Cmd.Add("hud_dump", HudDump);

            Cmd.Add("hud_describe", Describe);

            InitConfig();
        }

        private static void Describe()
        {
            Con.Print("--\n");
            Con.Print($"GL  | [{Scr.glX} {Scr.glY}] [{Scr.glWidth} {Scr.glHeight}]\n");
            Con.Print($"Ref | [{Render.RefDef.Vrect.x} {Render.RefDef.Vrect.y}] [{Render.RefDef.Vrect.width} {Render.RefDef.Vrect.height}]\n");
            Con.Print($"Hud | [{Width} {Height}]\n");
            Con.Print($"Con | [{Scr.ConLines} {Scr.ConCurrent}]\n");
            Con.Print("--\n");
        }

        private static void HudDump()
        {
            try
            {
                var json = new JavaScriptSerializer().Serialize(_hudEntry.Dto);
                File.WriteAllText($"{Common.GameDir}\\dump.hud", json);
            }
            catch (Exception ex)
            {
                Con.Print(ex.Message);
            }
        }

        private static HudEntry _hudEntry;

        private static string _hudConfig;

        private static float _scrHudScale;
        private static float _scrConScale;

        private static void InitConfig()
        {
            _hudConfig = HudConfig.String;

            if (string.IsNullOrWhiteSpace(_hudConfig) || _hudConfig.ToLower() == "default")
            {
                SetDefaultConfig();
                return;
            }

            var path = _hudConfig.EndsWith(".hud")
                ? _hudConfig
                : $"{_hudConfig}.hud";

            var bytes = Common.LoadFile(path);
            if (bytes == null)
            {
                Con.Print($"Couldn't load HUD config {path}\n");
                if (_hudEntry == null)
                    SetDefaultConfig();
                return;
            }
            var script = Encoding.ASCII.GetString(bytes);
            Con.Print($"Loading {path}\n");

            try
            {
                var dto = new JavaScriptSerializer().Deserialize<HudEntryDto>(script);
                _hudEntry = new Factory().Convert(dto);
            }
            catch (Exception ex)
            {
                Con.Print(ex.Message);
                SetDefaultConfig();
            }
            Scr.vid.recalc_refdef = true;
        }

        private static void SetDefaultConfig()
        {
            var entry = new HudEntryDto
            {
                Width = 640,
                Height = 480,
                Items = new[]
                {
                    new HudItemDto {X = 4, Y = 452, ItemType = nameof(HealthIconHudItem)},
                    new HudItemDto {X = 28, Y = 452, ItemType = nameof(HealthValueHudItem)},
                    new HudItemDto {X = 4, Y = 424, ItemType = nameof(ArmorIconHudItem)},
                    new HudItemDto {X = 28, Y = 424, ItemType = nameof(ArmorValueHudItem)},
                    new HudItemDto {X = 612, Y = 452, ItemType = nameof(AmmoIconHudItem)},
                    new HudItemDto {X = 540, Y = 452, ItemType = nameof(AmmoValueHudItem)},
                    new HudItemDto {X = 224, Y = 452, ItemType = nameof(HorizontalWeaponBarHudItem)}
                }
            };

            _hudEntry = new Factory().Convert(entry);
        }

        // Sbar_ShowScores
        //
        // Tab key down
        private static void ShowScores()
        {
            if (_showScores)
                return;
            _showScores = true;
        }


        // Sbar_DontShowScores
        //
        // Tab key up
        private static void DontShowScores()
        {
            _showScores = false;
        }

        public static void Draw()
        {
            if (Math.Abs(Scr.ConCurrent - Height) < 0.1)
                return;

            Scr.CopyEverithing = true;

            if (_showScores || Client.cl.stats[QStats.STAT_HEALTH] <= 0)
            {
                Drawer.DrawPic(0, 0, _scoreBar);
                Sbar.DrawScoreboard();
                return;
            }

            if (_hudConfig != HudConfig.String)
                InitConfig();

            if (View.Crosshair > 0)
                Drawer.DrawCharacter(Width / 2 - 4, Height / 2 - 4, '+');

            foreach (var item in _hudEntry.Items)
            {
                item.Draw();
            }
        }

        #region Big nums

        private const int NumWidth = 24;

        private const int NumHeight = 24;

        // Sbar_DrawNum
        public static void DrawNum(int x, int y, int num, int digits, int color)
        {
            var str = num.ToString();

            if (str.Length > digits)
                str = str.Remove(str.Length - digits);
            else if (str.Length < digits)
                x += (digits - str.Length) * 24;

            foreach (var t in str)
            {
                var frame = t == '-'
                    ? StatMinus
                    : t - '0';

                Drawer.DrawPic(x, y, _nums[color, frame]);
                x += NumWidth;
            }
        }

        #endregion
    }
}
