using System;

namespace Quarp.HudSystem
{
    [Serializable]
    internal sealed class HudEntryDto
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public HudItemDto[] Items { get; set; }
    }

    [Serializable]
    public class HudItemDto
    {
        public string ItemType;

        public int X;

        public int Y;
    }
}
