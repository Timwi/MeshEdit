using System.Collections.Generic;
using System.Linq;
using RT.Serialization;
using RT.Util.Geometry;

namespace MeshEdit
{
    sealed class Face
    {
        [ClassifyNotNull]
        public VertexInfo[] Vertices = new VertexInfo[0];
        public bool Hidden = false;

        public IEnumerable<Pt> Locations { get { return Vertices.Select(v => v.Location); } }
        public IEnumerable<PointD> Textures { get { return Vertices.Where(v => v.Texture != null).Select(v => v.Texture.Value); } }
        public IEnumerable<Pt> Normals { get { return Vertices.Where(v => v.Normal != null).Select(v => v.Normal.Value); } }

        public Face(VertexInfo[] vertices, bool hidden = false) { Vertices = vertices; Hidden = hidden; }
        public Face(Pt[] vertices, bool hidden = false) { Vertices = vertices.Select(v => new VertexInfo(v, null, null)).ToArray(); Hidden = hidden; }
        private Face() { } // Classify
    }
}
