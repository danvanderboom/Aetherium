using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Aetherium.Geometry
{
    public class GeometryHelper
    {
        public static double Distance(Plane plane, Vector3 point)
        {
            var line = new Line3(point, plane.Normal);

            var pointOnPlane = IntersectingPoint(plane, line);
            if (pointOnPlane == null)
                return double.NaN;

            return Distance(point, pointOnPlane.Value);
        }

        public static double Distance(Vector3 start, Vector3 end) =>
            Math.Sqrt(
                Math.Pow(end.X - start.X, 2)
                + Math.Pow(end.Y - start.Y, 2)
                + Math.Pow(end.Z - start.Z, 2));

        public static Vector3? IntersectingPoint(Plane plane, Line3 line) =>
            IntersectingPoint(plane.Point, plane.Normal, line.Point, plane.Normal);

        public static Vector3? IntersectingPoint(
            Vector3 planePoint, Vector3 planeNormal, Vector3 linePoint, Vector3 lineDirection)
        {
            var unitLineDirection = lineDirection.Normalize();

            if (planeNormal.Dot(unitLineDirection) == 0)
                return null;

            var t = (planeNormal.Dot(planePoint) - planeNormal.Dot(linePoint)) / planeNormal.Dot(unitLineDirection);
            return linePoint.Add(unitLineDirection.Scale(t));
        }
    }
}

