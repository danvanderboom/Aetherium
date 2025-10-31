using System;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class SeasonManagerTests
    {
        [Test]
        public void SeasonManager_GetSeason_ReturnsSpringForDay0()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });
            
            var seasonManager = new SeasonManager(options);
            
            var season = seasonManager.GetSeason(0);
            
            Assert.AreEqual("spring", season);
        }

        [Test]
        public void SeasonManager_GetSeason_CyclesThroughSeasons()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });
            
            var seasonManager = new SeasonManager(options);
            
            // Day 0-29: Spring
            Assert.AreEqual("spring", seasonManager.GetSeason(0));
            Assert.AreEqual("spring", seasonManager.GetSeason(15));
            Assert.AreEqual("spring", seasonManager.GetSeason(29));
            
            // Day 30-59: Summer
            Assert.AreEqual("summer", seasonManager.GetSeason(30));
            Assert.AreEqual("summer", seasonManager.GetSeason(45));
            Assert.AreEqual("summer", seasonManager.GetSeason(59));
            
            // Day 60-89: Fall
            Assert.AreEqual("fall", seasonManager.GetSeason(60));
            Assert.AreEqual("fall", seasonManager.GetSeason(75));
            Assert.AreEqual("fall", seasonManager.GetSeason(89));
            
            // Day 90-119: Winter
            Assert.AreEqual("winter", seasonManager.GetSeason(90));
            Assert.AreEqual("winter", seasonManager.GetSeason(105));
            Assert.AreEqual("winter", seasonManager.GetSeason(119));
            
            // Day 120: Back to spring (cycle)
            Assert.AreEqual("spring", seasonManager.GetSeason(120));
        }

        [Test]
        public void SeasonManager_GetSeason_ReturnsDefaultWhenDisabled()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = false
            });
            
            var seasonManager = new SeasonManager(options);
            
            var season = seasonManager.GetSeason(50); // Should be summer if enabled
            
            Assert.AreEqual("spring", season); // Default when disabled
        }

        [Test]
        public void SeasonManager_GetDayInSeason_ReturnsCorrectDay()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });
            
            var seasonManager = new SeasonManager(options);
            
            // Day 15 is day 15 in spring
            Assert.AreEqual(15, seasonManager.GetDayInSeason(15));
            
            // Day 45 is day 15 in summer
            Assert.AreEqual(15, seasonManager.GetDayInSeason(45));
            
            // Day 90 is day 0 in winter
            Assert.AreEqual(0, seasonManager.GetDayInSeason(90));
        }

        [Test]
        public void SeasonManager_GetYear_ReturnsCorrectYear()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });
            
            var seasonManager = new SeasonManager(options);
            
            // Days 0-119: Year 0
            Assert.AreEqual(0, seasonManager.GetYear(0));
            Assert.AreEqual(0, seasonManager.GetYear(60));
            Assert.AreEqual(0, seasonManager.GetYear(119));
            
            // Day 120: Year 1
            Assert.AreEqual(1, seasonManager.GetYear(120));
            
            // Day 240: Year 2
            Assert.AreEqual(2, seasonManager.GetYear(240));
        }

        [Test]
        public void SeasonManager_GetSpawnModifier_ReturnsModifierForSeason()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });
            
            var seasonManager = new SeasonManager(options);
            
            // Check that modifiers are returned (actual values depend on implementation)
            var modifier = seasonManager.GetSpawnModifier("wolf", "winter");
            Assert.GreaterOrEqual(modifier, 0.0);
        }
    }
}
