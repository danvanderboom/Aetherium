using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Rendering.Widgets;
using ConsoleGameModel;

namespace ConsoleGame.Rendering
{
    /// <summary>
    /// Manages widget lifecycle and visibility based on game state
    /// </summary>
    public class WidgetManager
    {
        private readonly Dictionary<string, IWidget> widgets = new Dictionary<string, IWidget>();
        private PerceptionDto? lastPerception;

        /// <summary>
        /// Register a widget with the manager
        /// </summary>
        public void RegisterWidget(IWidget widget)
        {
            widgets[widget.Id] = widget;
        }

        /// <summary>
        /// Unregister a widget
        /// </summary>
        public void UnregisterWidget(string widgetId)
        {
            widgets.Remove(widgetId);
        }

        /// <summary>
        /// Get all visible widgets in Z-order
        /// </summary>
        public IEnumerable<IWidget> GetVisibleWidgets()
        {
            return widgets.Values
                .Where(w => w.IsVisible)
                .OrderBy(w => w.ZOrder);
        }

        /// <summary>
        /// Get a specific widget by ID
        /// </summary>
        public IWidget? GetWidget(string widgetId)
        {
            return widgets.TryGetValue(widgetId, out var widget) ? widget : null;
        }

        /// <summary>
        /// Update widget visibility and state based on perception
        /// </summary>
        public void UpdateFromPerception(PerceptionDto perception)
        {
            lastPerception = perception;

            // Update compass visibility
            if (widgets.TryGetValue("compass", out var compassWidget))
            {
                var hasCompass = perception.NavigationData?.HasCompass ?? false;
                compassWidget.IsVisible = hasCompass;
            }

            // Update inventory widget if it exists
            if (widgets.TryGetValue("inventory", out var inventoryWidget))
            {
                inventoryWidget.IsVisible = perception.Inventory != null;
            }

            // Additional widget updates can be added here
        }

        /// <summary>
        /// Get all widgets as a dictionary for GameViewState
        /// </summary>
        public Dictionary<string, IWidget> GetAllWidgets()
        {
            return new Dictionary<string, IWidget>(widgets);
        }
    }
}

