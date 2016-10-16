using System;
using RT.Util;
using RT.Util.Geometry;

namespace MeshEdit
{
    public sealed class VertexInfo
    {
        public Pt Location;
        public PointD? Texture;
        public Pt? Normal;

        public VertexInfo(Pt location, PointD? texture, Pt? normal)
        {
            Location = location;
            Texture = texture;
            Normal = normal;
        }

        private VertexInfo() { } // Classify

        public override string ToString()
        {
            return Location.ToString() + Texture?.Apply(t => "; T=" + t) + Normal?.Apply(n => "; N=" + n);
        }
    }
}
