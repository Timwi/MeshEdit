using System.Collections.Generic;
using System.Linq;
using RT.Util.Geometry;
using RT.Util.Serialization;

namespace MeshEdit
{
    sealed class Face
    {
        [ClassifyNotNull]
        public VertexInfo[] Vertices = new VertexInfo[0];
        public bool Hidden = false;

        public IEnumerable<Pt> Locations { get { return Vertices.Select(v => v.Location); } }
        public IEnumerable<PointD> Textures { get { return Vertices.Select(v => v.Texture); } }
        public IEnumerable<Pt> Normals { get { return Vertices.Select(v => v.Normal); } }

        public Face(VertexInfo[] vertices, bool hidden = false) { Vertices = vertices; Hidden = hidden; }
        private Face() { } // Classify
    }
}
