using System;
using System.Linq;
using Aetherium.Rendering.Themes;
using Aetherium.Model;

namespace Aetherium.Rendering.Widgets
{
    /// <summary>
    /// Inventory widget showing player's items and capacity
    /// </summary>
    public class InventoryWidget : WidgetBase
    {
        private InventoryDto? inventoryData;
        private ThemeConfig theme;

        public InventoryWidget(ThemeConfig theme) : base("inventory")
        {
            this.theme = theme;
            ZOrder = 90; // Render below compass
        }

        /// <summary>
        /// Update inventory data from perception
        /// </summary>
        public void UpdateInventoryData(InventoryDto? data)
        {
            inventoryData = data;
            IsVisible = data != null && data.Items.Any();
        }

        public override object GetRenderData()
        {
            if (inventoryData == null)
            {
                return new InventoryRenderData
                {
                    Count = 0,
                    Capacity = 10,
                    Items = Array.Empty<string>()
                };
            }

            var items = inventoryData.Items
                .Select(item => string.IsNullOrEmpty(item.KeyId) 
                    ? item.Label 
                    : $"{item.Label} [{item.KeyId}]")
                .ToArray();

            return new InventoryRenderData
            {
                Count = inventoryData.Items.Count,
                Capacity = inventoryData.Capacity,
                Items = items
            };
        }

        /// <summary>
        /// Update the theme configuration
        /// </summary>
        public void UpdateTheme(ThemeConfig newTheme)
        {
            theme = newTheme;
        }
    }
}


