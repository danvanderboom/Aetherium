using System;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.Test
{
    public class LightingCoreTests
    {
        [Test]
        public void LightSource_Component_HasDefaultValues()
        {
            var lightSource = new LightSource();

            Assert.AreEqual(1.0, lightSource.Intensity);
            Assert.AreEqual(5, lightSource.Range);
            Assert.AreEqual(1.0, lightSource.Red);
            Assert.AreEqual(1.0, lightSource.Green);
            Assert.AreEqual(1.0, lightSource.Blue);
            Assert.IsTrue(lightSource.IsEnabled);
            Assert.IsFalse(lightSource.IsDynamic);
        }

        [Test]
        public void LightSource_Component_CanSetProperties()
        {
            var lightSource = new LightSource(0.5, 10);

            Assert.AreEqual(0.5, lightSource.Intensity);
            Assert.AreEqual(10, lightSource.Range);

            lightSource.Red = 1.0;
            lightSource.Green = 0.8;
            lightSource.Blue = 0.6;
            lightSource.IsEnabled = false;
            lightSource.IsDynamic = true;

            Assert.AreEqual(1.0, lightSource.Red);
            Assert.AreEqual(0.8, lightSource.Green);
            Assert.AreEqual(0.6, lightSource.Blue);
            Assert.IsFalse(lightSource.IsEnabled);
            Assert.IsTrue(lightSource.IsDynamic);
        }

        [Test]
        public void LightSource_Component_CanSetColorInConstructor()
        {
            var lightSource = new LightSource(0.8, 15, 1.0, 0.5, 0.0);

            Assert.AreEqual(0.8, lightSource.Intensity);
            Assert.AreEqual(15, lightSource.Range);
            Assert.AreEqual(1.0, lightSource.Red);
            Assert.AreEqual(0.5, lightSource.Green);
            Assert.AreEqual(0.0, lightSource.Blue);
        }

        [Test]
        public void Entity_CanHaveLightSource()
        {
            var entity = new Aetherium.Entities.LightEntity();
            var lightSource = new LightSource(1.0, 5);

            entity.Set(lightSource);

            Assert.IsTrue(entity.Has<LightSource>());
            var retrieved = entity.Get<LightSource>();
            Assert.AreEqual(lightSource, retrieved);
        }

        [Test]
        public void LightFrame_InitializesEmpty()
        {
            var frame = new Aetherium.Lighting.LightFrame();

            Assert.AreEqual(0, frame.LightLevels.Count);
        }

        [Test]
        public void LightFrame_SetLightLevel_StoresValue()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(5, 10, 0);

            frame.SetLightLevel(location, 0.7);

            Assert.AreEqual(0.7, frame.GetLightLevel(location));
        }

        [Test]
        public void LightFrame_SetLightLevel_ClampsToZeroToOne()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(0, 0, 0);

            frame.SetLightLevel(location, -0.5);
            Assert.AreEqual(0.0, frame.GetLightLevel(location));

            frame.SetLightLevel(location, 1.5);
            Assert.AreEqual(1.0, frame.GetLightLevel(location));
        }

        [Test]
        public void LightFrame_GetLightLevel_ReturnsZeroForUnknownLocation()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(100, 100, 0);

            Assert.AreEqual(0.0, frame.GetLightLevel(location));
        }

        [Test]
        public void LightFrame_AddLightLevel_AccumulatesLight()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(0, 0, 0);

            frame.AddLightLevel(location, 0.3);
            frame.AddLightLevel(location, 0.4);

            Assert.AreEqual(0.7, frame.GetLightLevel(location), 0.001);
        }

        [Test]
        public void LightFrame_AddLightLevel_ClampsWhenExceedsOne()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(0, 0, 0);

            frame.AddLightLevel(location, 0.6);
            frame.AddLightLevel(location, 0.6); // Would exceed 1.0

            Assert.AreEqual(1.0, frame.GetLightLevel(location), 0.001);
        }

        [Test]
        public void LightFrame_SetLightLevel_RemovesZeroLevels()
        {
            var frame = new Aetherium.Lighting.LightFrame();
            var location = new WorldLocation(0, 0, 0);

            frame.SetLightLevel(location, 0.5);
            Assert.IsTrue(frame.LightLevels.ContainsKey(location));

            frame.SetLightLevel(location, 0.0);
            Assert.IsFalse(frame.LightLevels.ContainsKey(location));
        }
    }
}


