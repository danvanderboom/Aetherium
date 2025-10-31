using System;
using Aetherium.Rendering.Themes;
using Aetherium.Model;

namespace Aetherium.Rendering.Widgets
{
    /// <summary>
    /// Compass widget showing player heading with arrow or degree display
    /// </summary>
    public class CompassWidget : WidgetBase
    {
        private CompassMode currentMode = CompassMode.Arrow;
        private NavigationDataDto? navigationData;
        private bool isDirectionalVision;
        private int fovDegrees = 360;
        private ThemeConfig theme;

        public CompassWidget(ThemeConfig theme) : base("compass")
        {
            this.theme = theme;
            ZOrder = 100; // Render on top
        }

        /// <summary>
        /// Update navigation data from perception
        /// </summary>
        public void UpdateNavigationData(NavigationDataDto? data, bool directionalVision, int fieldOfView)
        {
            navigationData = data;
            isDirectionalVision = directionalVision;
            fovDegrees = fieldOfView;
            IsVisible = data?.HasCompass ?? false;
        }

        /// <summary>
        /// Toggle between arrow and degree display modes
        /// </summary>
        public void ToggleMode()
        {
            currentMode = currentMode == CompassMode.Arrow ? CompassMode.Degree : CompassMode.Arrow;
        }

        /// <summary>
        /// Get the current display mode
        /// </summary>
        public CompassMode Mode => currentMode;

        public override object GetRenderData()
        {
            if (navigationData == null || !navigationData.HasCompass)
            {
                return new CompassRenderData
                {
                    Mode = currentMode,
                    Heading = 0,
                    DirectionName = "Unknown",
                    DirectionSymbol = "?",
                    IsDirectionalVision = isDirectionalVision,
                    FieldOfViewDegrees = fovDegrees
                };
            }

            var heading = navigationData.HeadingDegrees;
            var direction = navigationData.CardinalDirection;

            return new CompassRenderData
            {
                Mode = currentMode,
                Heading = heading,
                DirectionName = GetDirectionName(direction),
                DirectionSymbol = GetDirectionSymbol(heading, direction),
                IsDirectionalVision = isDirectionalVision,
                FieldOfViewDegrees = fovDegrees
            };
        }

        private string GetDirectionName(Aetherium.Model.WorldDirection direction)
        {
            return direction switch
            {
                Aetherium.Model.WorldDirection.North => "North",
                Aetherium.Model.WorldDirection.East => "East",
                Aetherium.Model.WorldDirection.South => "South",
                Aetherium.Model.WorldDirection.West => "West",
                Aetherium.Model.WorldDirection.Up => "Up",
                Aetherium.Model.WorldDirection.Down => "Down",
                _ => "Unknown"
            };
        }

        private string GetDirectionSymbol(int degrees, Aetherium.Model.WorldDirection cardinalDirection)
        {
            // For now, use cardinal directions only (4-way compass)
            // Future enhancement: support 8-way compass with intermediate directions
            return cardinalDirection switch
            {
                Aetherium.Model.WorldDirection.North => theme.GetSymbol("compass_n", "↑"),
                Aetherium.Model.WorldDirection.East => theme.GetSymbol("compass_e", "→"),
                Aetherium.Model.WorldDirection.South => theme.GetSymbol("compass_s", "↓"),
                Aetherium.Model.WorldDirection.West => theme.GetSymbol("compass_w", "←"),
                Aetherium.Model.WorldDirection.Up => "▲",
                Aetherium.Model.WorldDirection.Down => "▼",
                _ => "?"
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


