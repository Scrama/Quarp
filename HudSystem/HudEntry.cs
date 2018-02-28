using System.Linq;

namespace Quarp.HudSystem
{
    internal sealed class HudEntry
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public HudItem[] Items { get; set; }

        public HudEntryDto Dto
        {
            get
            {
                return new HudEntryDto
                {
                    Width = Width,
                    Height = Height,
                    Items = Items.Select(x => x.Dto).ToArray()
                };
            }
        }
    }
}
