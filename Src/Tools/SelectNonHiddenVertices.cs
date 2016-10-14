using System.Linq;

namespace MeshEdit.Tools
{
    sealed class SelectNonHiddenVertices : Tool
    {
        public override string Name => "Select non-hidden vertices";
        public static SelectNonHiddenVertices Instance = new SelectNonHiddenVertices();
        private SelectNonHiddenVertices() { }

        public override void Execute()
        {
            Program.Settings.SelectedVertices = Program.Settings.Faces
                .SelectMany(f => f.Vertices.Select(v => new { Face = f, Vertex = v }))
                .GroupBy(inf => inf.Vertex.Location)
                .Where(gr => gr.All(inf => !inf.Face.Hidden))
                .SelectMany(gr => gr)
                .Select(inf => inf.Vertex.Location)
                .Distinct()
                .ToList();
            Program.Settings.IsFaceSelected = false;
        }
    }
}
