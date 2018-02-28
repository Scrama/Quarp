namespace Quarp.HudSystem
{
    internal class HudItem
    {
        protected readonly HudItemDto Data;

        internal HudItem(HudItemDto data)
        {
            Data = data;
        }

        public virtual void Draw()
        {
        }

        public HudItemDto Dto => Data;
    }

    #region Health

    /// <summary>
    /// Health value
    /// </summary>
    internal sealed class HealthValueHudItem : HudItem
    {
        internal HealthValueHudItem(HudItemDto data) : base(data)
        {
        }

        public override void Draw()
        {
            Hud.DrawNum(
                Data.X,
                Data.Y,
                Client.cl.stats[QStats.STAT_HEALTH],
                3,
                Client.cl.stats[QStats.STAT_HEALTH] <= 25 ? 1 : 0
            );
        }
    }

    /// <summary>
    /// The Face
    /// </summary>
    internal sealed class HealthIconHudItem : HudItem
    {
        public override void Draw()
        {
            var cl = Client.cl;

            int f;

            if (cl.HasItems(QItems.IT_INVISIBILITY | QItems.IT_INVULNERABILITY))
            {
                Drawer.DrawPic(Data.X, Data.Y, Hud.FaceInvisInvuln);
                return;
            }
            if (cl.HasItems(QItems.IT_QUAD))
            {
                Drawer.DrawPic(Data.X, Data.Y, Hud.FaceQuad);
                return;
            }
            if (cl.HasItems(QItems.IT_INVISIBILITY))
            {
                Drawer.DrawPic(Data.X, Data.Y, Hud.FaceInvis);
                return;
            }
            if (cl.HasItems(QItems.IT_INVULNERABILITY))
            {
                Drawer.DrawPic(Data.X, Data.Y, Hud.FaceQuad);
                return;
            }

            if (cl.stats[QStats.STAT_HEALTH] >= 100)
                f = 4;
            else
                f = cl.stats[QStats.STAT_HEALTH] / 20;

            var anim = cl.time <= cl.faceanimtime ? 1 : 0;

            Drawer.DrawPic(Data.X, Data.Y, Hud.Faces[f, anim]);
        }

        internal HealthIconHudItem(HudItemDto data) : base(data)
        {
        }
    }

    #endregion

    #region Armor

    /// <summary>
    /// Armor value
    /// </summary>
    internal sealed class ArmorValueHudItem : HudItem
    {
        public override void Draw()
        {
            if (Client.cl.HasItems(QItems.IT_INVULNERABILITY))
            {
                Hud.DrawNum(
                    Data.X,
                    Data.Y,
                    666,
                    3,
                    1
                );
            }
            else
            {
                if (Client.cl.stats[QStats.STAT_ARMOR] > 0)
                    Hud.DrawNum(
                        Data.X,
                        Data.Y,
                        Client.cl.stats[QStats.STAT_ARMOR],
                        3,
                        Client.cl.stats[QStats.STAT_ARMOR] <= 25 ? 1 : 0
                    );
            }
        }

        internal ArmorValueHudItem(HudItemDto data) : base(data)
        {
        }
    }

    /// <summary>
    /// Jacket
    /// </summary>
    internal sealed class ArmorIconHudItem : HudItem
    {
        public override void Draw()
        {
            var cl = Client.cl;

            if (cl.HasItems(QItems.IT_INVULNERABILITY))
            {
                Drawer.DrawPic(Data.X, Data.Y, Drawer.Disc);
                return;
            }

            if (cl.HasItems(QItems.IT_ARMOR3))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Armor[2]);
            else if (cl.HasItems(QItems.IT_ARMOR2))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Armor[1]);
            else if (cl.HasItems(QItems.IT_ARMOR1))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Armor[0]);
        }

        internal ArmorIconHudItem(HudItemDto data) : base(data)
        {
        }
    }

    #endregion

    #region Ammo


    /// <summary>
    /// Health value
    /// </summary>
    internal sealed class AmmoValueHudItem : HudItem
    {
        public override void Draw()
        {
            if (Client.cl.HasAny(Hud.AmmoConsts))
                Hud.DrawNum(
                    Data.X,
                    Data.Y,
                    Client.cl.stats[QStats.STAT_AMMO],
                    3,
                    Client.cl.stats[QStats.STAT_AMMO] <= 10 ? 1 : 0
                );
        }

        internal AmmoValueHudItem(HudItemDto data) : base(data)
        {
        }
    }

    /// <summary>
    /// The Face
    /// </summary>
    internal sealed class AmmoIconHudItem : HudItem
    {
        public override void Draw()
        {
            var cl = Client.cl;

            if (cl.HasItems(QItems.IT_SHELLS))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Ammo[0]);
            else if (cl.HasItems(QItems.IT_NAILS))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Ammo[1]);
            else if (cl.HasItems(QItems.IT_ROCKETS))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Ammo[2]);
            else if (cl.HasItems(QItems.IT_CELLS))
                Drawer.DrawPic(Data.X, Data.Y, Hud.Ammo[3]);
        }

        internal AmmoIconHudItem(HudItemDto data) : base(data)
        {
        }
    }

    #endregion

    #region Horizontal inventory

    /// <summary>
    /// Armor value
    /// </summary>
    internal sealed class HorizontalWeaponBarHudItem : HudItem
    {
        public override void Draw()
        {
            var cl = Client.cl;

            // icons
            for (var i = 0; i < 7; ++i)
            {
                // shift from SG up to RL
                if (!cl.HasItems(QItems.IT_SHOTGUN << i))
                    continue;

                var time = cl.item_gettime[i];
                var flashon = (int)((cl.time - time) * 10);
                if (flashon >= 10)
                    flashon = cl.stats[QStats.STAT_ACTIVEWEAPON] == QItems.IT_SHOTGUN << i ? 1 : 0;
                else
                    flashon = flashon % 5 + 2;

                Drawer.DrawPic(Data.X + i * 24, Data.Y + 8, Hud.Weapons[flashon, i]);
            }

            // ammo counts
            for (var i = 0; i < 4; ++i)
            {
                var num = cl.stats[QStats.STAT_SHELLS + i].ToString().PadLeft(3);
                if (num[0] != ' ')
                    Drawer.DrawCharacter(Data.X + (6 * i + 1) * 8 - 2, Data.Y, 18 + num[0] - '0');
                if (num[1] != ' ')
                    Drawer.DrawCharacter(Data.X + (6 * i + 2) * 8 - 2, Data.Y, 18 + num[1] - '0');
                if (num[2] != ' ')
                    Drawer.DrawCharacter(Data.X + (6 * i + 3) * 8 - 2, Data.Y, 18 + num[2] - '0');
            }
        }

        internal HorizontalWeaponBarHudItem(HudItemDto data) : base(data)
        {
        }
    }

    #endregion
}
