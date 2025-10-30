using Xunit;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Test
{
    public class StrategicComponentsTests
    {
        [Fact]
        public void Consumable_Defaults_SetCorrectly()
        {
            var consumable = new Consumable();
            Assert.Equal(ConsumableEffectType.HealthRestore, consumable.EffectType);
            Assert.Equal(1, consumable.EffectValue);
            Assert.Equal(1, consumable.Uses);
        }

        [Fact]
        public void Consumable_Properties_CanBeSet()
        {
            var consumable = new Consumable
            {
                EffectType = ConsumableEffectType.EnergyRestore,
                EffectValue = 50,
                Uses = 3
            };
            Assert.Equal(ConsumableEffectType.EnergyRestore, consumable.EffectType);
            Assert.Equal(50, consumable.EffectValue);
            Assert.Equal(3, consumable.Uses);
        }

        [Fact]
        public void Activatable_Defaults_SetCorrectly()
        {
            var activatable = new Activatable();
            Assert.False(activatable.IsActivated);
            Assert.NotNull(activatable.TargetEntityIds);
            Assert.Empty(activatable.TargetEntityIds);
            Assert.False(activatable.ToggleBehavior);
        }

        [Fact]
        public void Activatable_CanHaveTargets()
        {
            var activatable = new Activatable
            {
                TargetEntityIds = { "door1", "door2" }
            };
            Assert.Equal(2, activatable.TargetEntityIds.Count);
            Assert.Contains("door1", activatable.TargetEntityIds);
        }

        [Fact]
        public void PressureSensitive_Defaults_SetCorrectly()
        {
            var pressure = new PressureSensitive();
            Assert.Equal(1, pressure.WeightThreshold);
            Assert.False(pressure.IsPressed);
            Assert.NotNull(pressure.TargetEntityIds);
        }

        [Fact]
        public void PressureSensitive_CanSetThreshold()
        {
            var pressure = new PressureSensitive
            {
                WeightThreshold = 5,
                IsPressed = true,
                TargetEntityIds = { "trap1" }
            };
            Assert.Equal(5, pressure.WeightThreshold);
            Assert.True(pressure.IsPressed);
            Assert.Contains("trap1", pressure.TargetEntityIds);
        }

        [Fact]
        public void Hidden_Defaults_SetCorrectly()
        {
            var hidden = new Hidden();
            Assert.True(hidden.IsHidden);
            Assert.Equal(0.5, hidden.DiscoveryDifficulty);
        }

        [Fact]
        public void Hidden_DiscoveryDifficulty_ClampedRange()
        {
            var hidden = new Hidden
            {
                DiscoveryDifficulty = 0.8
            };
            Assert.Equal(0.8, hidden.DiscoveryDifficulty);
        }

        [Fact]
        public void Climbable_Defaults_SetCorrectly()
        {
            var climbable = new Climbable();
            Assert.Equal(ClimbDirection.Both, climbable.Direction);
            Assert.False(climbable.RequiresItem);
            Assert.Null(climbable.RequiredItemId);
        }

        [Fact]
        public void Climbable_CanRequireItem()
        {
            var climbable = new Climbable
            {
                Direction = ClimbDirection.Up,
                RequiresItem = true,
                RequiredItemId = "rope1"
            };
            Assert.Equal(ClimbDirection.Up, climbable.Direction);
            Assert.True(climbable.RequiresItem);
            Assert.Equal("rope1", climbable.RequiredItemId);
        }

        [Fact]
        public void CapacityBoost_Defaults_SetCorrectly()
        {
            var capacity = new CapacityBoost();
            Assert.Equal(5, capacity.AdditionalCapacity);
        }

        [Fact]
        public void CapacityBoost_CanSetCapacity()
        {
            var capacity = new CapacityBoost
            {
                AdditionalCapacity = 10
            };
            Assert.Equal(10, capacity.AdditionalCapacity);
        }

        [Fact]
        public void PlaceableLight_Defaults_SetCorrectly()
        {
            var placeable = new PlaceableLight();
            Assert.False(placeable.IsPlaced);
        }

        [Fact]
        public void ProvidesNavigation_Defaults_SetCorrectly()
        {
            var nav = new ProvidesNavigation();
            Assert.False(nav.RevealsArea);
            Assert.Null(nav.DirectionToTarget);
        }

        [Fact]
        public void ProvidesNavigation_CanSetTarget()
        {
            var target = new WorldLocation(10, 20, 0);
            var nav = new ProvidesNavigation
            {
                RevealsArea = true,
                DirectionToTarget = target
            };
            Assert.True(nav.RevealsArea);
            Assert.Equal(target, nav.DirectionToTarget);
        }

        [Fact]
        public void ForcesDoor_Defaults_SetCorrectly()
        {
            var forces = new ForcesDoor();
            Assert.Equal(1, forces.Strength);
            Assert.Equal(10, forces.Durability);
        }

        [Fact]
        public void ForcesDoor_CanSetStrengthAndDurability()
        {
            var forces = new ForcesDoor
            {
                Strength = 5,
                Durability = 20
            };
            Assert.Equal(5, forces.Strength);
            Assert.Equal(20, forces.Durability);
        }

        [Fact]
        public void Lockpick_Defaults_SetCorrectly()
        {
            var lockpick = new Lockpick();
            Assert.Equal(1, lockpick.SkillLevel);
            Assert.Equal(10, lockpick.Durability);
        }

        [Fact]
        public void Lockpick_CanSetSkillAndDurability()
        {
            var lockpick = new Lockpick
            {
                SkillLevel = 7,
                Durability = 5
            };
            Assert.Equal(7, lockpick.SkillLevel);
            Assert.Equal(5, lockpick.Durability);
        }

        [Fact]
        public void EnergyStorage_Defaults_SetCorrectly()
        {
            var energy = new EnergyStorage();
            Assert.Equal(100, energy.EnergyLevel);
            Assert.Equal(100, energy.MaxEnergy);
            Assert.Equal(1, energy.ConsumesPerUse);
        }

        [Fact]
        public void EnergyStorage_Properties_CanBeSet()
        {
            var energy = new EnergyStorage
            {
                EnergyLevel = 75,
                MaxEnergy = 100,
                ConsumesPerUse = 5
            };
            Assert.Equal(75, energy.EnergyLevel);
            Assert.Equal(100, energy.MaxEnergy);
            Assert.Equal(5, energy.ConsumesPerUse);
        }

        [Fact]
        public void DataStorage_Defaults_SetCorrectly()
        {
            var storage = new DataStorage();
            Assert.Equal(string.Empty, storage.DataContent);
            Assert.False(storage.IsEncrypted);
        }

        [Fact]
        public void DataStorage_CanStoreContent()
        {
            var storage = new DataStorage
            {
                DataContent = "Important data",
                IsEncrypted = true
            };
            Assert.Equal("Important data", storage.DataContent);
            Assert.True(storage.IsEncrypted);
        }
    }
}
