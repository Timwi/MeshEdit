using System;
using System.Linq;
using RT.Util.Geometry;

namespace MeshEdit.Tools
{
    sealed class RecalculateTexturesKTANE : Tool
    {
        public override string Name => "Recalculate texture coordinates (KTANE component)";
        public static readonly RecalculateTexturesKTANE Instance = new RecalculateTexturesKTANE();
        private RecalculateTexturesKTANE() { }

        public override void Execute()
        {
            Program.Settings.Execute(new undo(
                Program.Settings.Faces
                    .SelectMany(f => f.Vertices)
                    .Where(v => Program.Settings.Faces.Where(f => f.Locations.Contains(v.Location)).All(f => !f.Hidden))
                    .Select(v => Tuple.Create(v, v.Texture, new PointD(.4771284794 * v.Location.X + .46155, -.4771284794 * v.Location.Z + .5337373145)))
                    .ToArray()));
        }

        sealed class undo : UndoItem
        {
            Tuple<VertexInfo, PointD?, PointD>[] _data;

            public undo(Tuple<VertexInfo, PointD?, PointD>[] data) { _data = data; }
            private undo() { } // Classify

            public override void Undo()
            {
                foreach (var tup in _data)
                    tup.Item1.Texture = tup.Item2;
            }

            public override void Redo()
            {
                foreach (var tup in _data)
                    tup.Item1.Texture = tup.Item3;
            }
        }
    }
}
