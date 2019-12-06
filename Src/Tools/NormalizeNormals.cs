using System;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Normalize normals")]
        public static void NormalizeNormals()
        {
            Program.Settings.Execute(new NormalizeNormalsUndo(Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => Program.Settings.SelectedVertices.Contains(v.Location))).ToArray()));
        }
    }

    sealed class NormalizeNormalsUndo : UndoItem
    {
        readonly Tuple<VertexInfo, Pt?, Pt?>[] _data;

        public NormalizeNormalsUndo(VertexInfo[] data) { _data = data.Select(v => new Tuple<VertexInfo, Pt?, Pt?>(v, v.Normal, v.Normal?.Normalize())).ToArray(); }
        private NormalizeNormalsUndo() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _data)
                tup.Item1.Normal = tup.Item2;
        }

        public override void Redo()
        {
            foreach (var tup in _data)
                tup.Item1.Normal = tup.Item3;
        }
    }
}
