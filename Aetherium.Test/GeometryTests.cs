using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Aetherium.Geometry;

namespace Aetherium.Test
{
    public class GeometryTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void DistanceBetweenPointAndPlane()
        {
            var point1 = GeometryHelper.IntersectingPoint(
                new Plane(point: new Vector3(0, 0, 0), normal: new Vector3(0, 0, 1)),
                new Line3(point: new Vector3(20, 20, 10), direction: new Vector3(0, 0, 1)));

            Assert.AreEqual(new Vector3(20, 20, 0), point1);

            var point2 = GeometryHelper.IntersectingPoint(
                new Plane(point: new Vector3(0, 0, 0), normal: new Vector3(0, 1, 0)),
                new Line3(point: new Vector3(20, 0, 10), direction: new Vector3(0, 1, 0)));

            Assert.AreEqual(new Vector3(20, 0, 10), point2);

            var point3 = GeometryHelper.IntersectingPoint(
                new Plane(point: new Vector3(0, 0, 0), normal: new Vector3(1, 0, 0)),
                new Line3(point: new Vector3(0, 20, 10), direction: new Vector3(1, 0, 0)));

            Assert.AreEqual(new Vector3(0, 20, 10), point3);
        }
    }
}

