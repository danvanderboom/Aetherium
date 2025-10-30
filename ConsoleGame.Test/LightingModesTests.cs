using System;
using System.Drawing;
using ConsoleGame.Lighting;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGameModel;
using Xunit;

namespace ConsoleGame.Test
{
    public class LightingModesTests
    {
        [Fact]
        public void SunlightCalculator_CalculatesSunPosition_Correctly()
        {
            // Arrange
            var calculator = new SunlightCalculator();

            // Act & Assert - Midnight (0 hours)
            var (azimuthMidnight, elevationMidnight) = calculator.CalculateSunPosition(0.0);
            Assert.Equal(0.0, azimuthMidnight, 0.1);
            Assert.True(elevationMidnight < -80.0); // Sun is below horizon

            // Act & Assert - Noon (12 hours)
            var (azimuthNoon, elevationNoon) = calculator.CalculateSunPosition(12.0);
            Assert.Equal(180.0, azimuthNoon, 0.1);
            Assert.True(elevationNoon > 80.0); // Sun is high in sky

            // Act & Assert - 6am (6 hours)
            var (azimuth6am, elevation6am) = calculator.CalculateSunPosition(6.0);
            Assert.Equal(90.0, azimuth6am, 0.1);
            Assert.True(elevation6am > -10.0 && elevation6am < 10.0); // Near horizon
        }

        [Fact]
        public void SunlightCalculator_GetsSunlightColor_WithCorrectIntensity()
        {
            // Arrange
            var calculator = new SunlightCalculator();

            // Act & Assert - Below horizon
            var (r1, g1, b1, intensity1) = calculator.GetSunlightColor(-20.0);
            Assert.Equal(0.0, intensity1);

            // Act & Assert - Sunrise (low elevation)
            var (r2, g2, b2, intensity2) = calculator.GetSunlightColor(5.0);
            Assert.True(r2 >= g2 && r2 >= b2); // Reddish tint
            Assert.True(intensity2 > 0.0 && intensity2 < 1.0);

            // Act & Assert - High noon
            var (r3, g3, b3, intensity3) = calculator.GetSunlightColor(60.0);
            Assert.Equal(1.0, r3);
            Assert.Equal(1.0, g3);
            Assert.Equal(1.0, b3);
            Assert.True(intensity3 >= 0.9);
        }

        [Fact]
        public void LightingSystem_SupportsTorchMode()
        {
            // Arrange
            var world = new World();
            var playerLocation = new WorldLocation(5, 5, 0);
            var bounds = new Rectangle(0, 0, 10, 10);
            var lightingSystem = new LightingSystem();

            // Act
            var lightFrame = lightingSystem.ComputeLightingWithMode(
                world,
                bounds,
                0, // z-level
                LightingMode.Torch,
                playerLocation,
                12.0); // time of day

            // Assert
            Assert.NotNull(lightFrame);
            var playerLightLevel = lightFrame.GetLightLevel(playerLocation);
            Assert.True(playerLightLevel > 0.5); // Player should be well-lit
        }

        [Fact]
        public void LightingSystem_SupportsSunlightMode()
        {
            // Arrange
            var world = new World();
            var bounds = new Rectangle(0, 0, 10, 10);
            var lightingSystem = new LightingSystem();

            // Act - Daylight (noon)
            var lightFrameDay = lightingSystem.ComputeLightingWithMode(
                world,
                bounds,
                0,
                LightingMode.Sunlight,
                null,
                12.0); // noon

            // Act - Night
            var lightFrameNight = lightingSystem.ComputeLightingWithMode(
                world,
                bounds,
                0,
                LightingMode.Sunlight,
                null,
                0.0); // midnight

            // Assert
            Assert.NotNull(lightFrameDay);
            Assert.NotNull(lightFrameNight);
            
            // Day should have more light than night
            var dayLightCount = lightFrameDay.LightLevels.Count;
            var nightLightCount = lightFrameNight.LightLevels.Count;
            Assert.True(dayLightCount > nightLightCount);
        }

        [Fact]
        public void HeatSignature_InitializesWithCorrectValues()
        {
            // Arrange & Act
            var heatSig = new HeatSignature(0.75, TimeSpan.FromSeconds(8));

            // Assert
            Assert.Equal(0.75, heatSig.Intensity);
            Assert.Equal(TimeSpan.FromSeconds(8), heatSig.Duration);
        }

        [Fact]
        public void HeatSignature_ClampsIntensityToRange()
        {
            // Arrange & Act
            var heatSigTooHigh = new HeatSignature(1.5, TimeSpan.FromSeconds(5));
            var heatSigTooLow = new HeatSignature(-0.5, TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(1.0, heatSigTooHigh.Intensity);
            Assert.Equal(0.0, heatSigTooLow.Intensity);
        }
    }
}

