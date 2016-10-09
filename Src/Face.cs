using System;
using RT.Util.Serialization;

namespace MeshEdit
{
    sealed class Face
    {
        [ClassifyNotNull]
        public Pt[] Vertices { get; private set; } = new Pt[0];
        public bool Hidden { get; private set; } = false;

        public Face(Pt[] vertices, bool hidden = false) { Vertices = vertices; Hidden = hidden; }
        private Face() { } // Classify

        public Face FlipHidden() => new Face(Vertices, !Hidden);
    }
}
