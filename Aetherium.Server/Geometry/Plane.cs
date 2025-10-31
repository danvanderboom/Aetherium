using System.Linq;
using System.Text;

namespace Aetherium.Geometry
{
    // TODO: use System.Numerics.Plane
    public struct Plane
    {
        public Vector3 Point { get; set; }
        public Vector3 Normal { get; set; }

        public Plane(Vector3 point, Vector3 normal)
        {
            Point = point;
            Normal = normal;
        }
    }
}

