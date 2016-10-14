﻿using System;
using System.Linq;

namespace MeshEdit.Tools
{
    sealed class RecalculateNormals : Tool
    {
        public override string Name => "Recalculate normals";
        public static readonly RecalculateNormals Instance = new RecalculateNormals();
        private RecalculateNormals() { }

        public override void Execute()
        {
            Program.Settings.Execute(new undo(
                Program.Settings.Faces
                    .SelectMany(f => f.Vertices.Select((v, i) => new { Face = f, Vertex = v, Index = i }))
                    .Where(inf => Program.Settings.SelectedVertices.Contains(inf.Vertex.Location))
                    .Select(inf => Tuple.Create(inf.Vertex, inf.Vertex.Normal, vectorCrossProduct(
                        inf.Face.Vertices[(inf.Index + inf.Face.Vertices.Length - 1) % inf.Face.Vertices.Length].Location - inf.Vertex.Location,
                        inf.Face.Vertices[(inf.Index + 1) % inf.Face.Vertices.Length].Location - inf.Vertex.Location)))
                    .GroupBy(inf => inf.Item1.Location)
                    .Select(gr => new { Group = gr, Location = gr.Key, NewNormal = gr.Select(inf => inf.Item3).Aggregate((prev, next) => prev + next) / gr.Count() })
                    .SelectMany(inf => inf.Group.Select(tup => Tuple.Create(tup.Item1, tup.Item2, inf.NewNormal)))
                    .ToArray()));
        }

        private Pt vectorCrossProduct(Pt u, Pt v)
        {
            return -new Pt(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);
        }

        sealed class undo : UndoItem
        {
            Tuple<VertexInfo, Pt?, Pt>[] _data;

            public undo(Tuple<VertexInfo, Pt?, Pt>[] data) { _data = data; }
            private undo() { } // Classify

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
}