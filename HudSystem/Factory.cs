using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarp.HudSystem
{
    internal sealed class Factory
    {
        private readonly Dictionary<string, Func<HudItemDto, HudItem>> _map =
            new Dictionary<string, Func<HudItemDto, HudItem>>
            {
                {nameof(HealthIconHudItem), dto => new HealthIconHudItem(dto)},
                {nameof(HealthValueHudItem), dto => new HealthValueHudItem(dto)},
                {nameof(ArmorValueHudItem), dto => new ArmorValueHudItem(dto)},
                {nameof(ArmorIconHudItem), dto => new ArmorIconHudItem(dto)},
                {nameof(AmmoValueHudItem), dto => new AmmoValueHudItem(dto)},
                {nameof(AmmoIconHudItem), dto => new AmmoIconHudItem(dto)},
                {nameof(HorizontalWeaponBarHudItem), dto => new HorizontalWeaponBarHudItem(dto)}
            };

        public HudEntry Convert(HudEntryDto dto)
        {
            return new HudEntry
            {
                Width = dto.Width,
                Height = dto.Height,
                Items = Build(dto.Items)
            };
        }

        private HudItem[] Build(HudItemDto[] src)
        {
            var list = new List<HudItem>();

            foreach (var dto in src)
            {
                if (_map.TryGetValue(dto.ItemType, out var make))
                {
                    //rescale screen position
                    list.Add(make(dto));
                }
                else
                {
                    Con.Print($"Unknown HUD item type {dto.ItemType}");
                }
            }
            return list.ToArray();
        }
    }
}
