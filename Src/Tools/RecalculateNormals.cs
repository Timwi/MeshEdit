using System;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Recalculate normals")]
        public static void RecalculateNormals(
            [ToolBool(
                "• Choose “Straight” to calculate the actual normal of each affected face.\n•Choose “Average” to average the normals of all the faces at each vertex.",
                "Straight",
                "Average")]
            bool average)
        {
            var a = Program.Settings.Faces
                .SelectMany(f => f.Vertices.Select((v, i) => new { Face = f, Vertex = v, Index = i }))
                .Where(inf => Program.Settings.Faces.Where(f => f.Locations.Contains(inf.Vertex.Location)).All(f => !f.Hidden) && (Program.Settings.SelectedVertices.Count == 0 || Program.Settings.SelectedVertices.Contains(inf.Vertex.Location)))
                .Select(inf => Tuple.Create(inf.Vertex, inf.Vertex.Normal,
                    (inf.Face.Vertices[(inf.Index + 1) % inf.Face.Vertices.Length].Location - inf.Vertex.Location) *
                    (inf.Face.Vertices[(inf.Index + inf.Face.Vertices.Length - 1) % inf.Face.Vertices.Length].Location - inf.Vertex.Location)));

            if (average)
            {
                a = a
                    .GroupBy(inf => inf.Item1.Location)
                    .Select(gr => new { Group = gr, Location = gr.Key, NewNormal = gr.Select(inf => inf.Item3).Aggregate((prev, next) => prev + next) / gr.Count() })
                    .SelectMany(inf => inf.Group.Select(tup => Tuple.Create(tup.Item1, tup.Item2, inf.NewNormal)));
            }

            Program.Settings.Execute(new RecalculateNormalsUndo(a.ToArray()));
        }
    }

    sealed class RecalculateNormalsUndo : UndoItem
    {
        Tuple<VertexInfo, Pt?, Pt>[] _data;

        public RecalculateNormalsUndo(Tuple<VertexInfo, Pt?, Pt>[] data) { _data = data; }
        private RecalculateNormalsUndo() { } // Classify

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
