using System;
using System.Collections.Generic;

namespace Quarp.HUD
{
    internal sealed class Factory
    {
        private readonly Dictionary<string, Func<HudDto, HudItem>> _map =
            new Dictionary<string, Func<HudDto, HudItem>>
            {
                {nameof(HealthIconHudItem), dto => new HealthIconHudItem(dto)},
                {nameof(HealthValueHudItem), dto => new HealthValueHudItem(dto)},
                {nameof(ArmorValueHudItem), dto => new ArmorValueHudItem(dto)},
                {nameof(ArmorIconHudItem), dto => new ArmorIconHudItem(dto)},
                {nameof(AmmoValueHudItem), dto => new AmmoValueHudItem(dto)},
                {nameof(AmmoIconHudItem), dto => new AmmoIconHudItem(dto)},
                {nameof(HorizontalWeaponBarHudItem), dto => new HorizontalWeaponBarHudItem(dto)}
            };

        public HudItem[] Build(HudDto[] src)
        {
            var list = new List<HudItem>();
            foreach (var dto in src)
            {
                if (_map.TryGetValue(dto.ItemType, out var make))
                    list.Add(make(dto));
                else
                    Con.Print($"Unknown HUD item type {dto.ItemType}");
            }
            return list.ToArray();
        }
    }
}
