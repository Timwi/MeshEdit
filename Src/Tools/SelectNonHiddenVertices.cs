using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Select non-hidden vertices")]
        public static void SelectNonHiddenVertices()
        {
            Program.Settings.SelectVertices(Program.Settings.Faces
                .SelectMany(f => f.Vertices.Select(v => new { Face = f, Vertex = v }))
                .GroupBy(inf => inf.Vertex.Location)
                .Where(gr => gr.All(inf => !inf.Face.Hidden))
                .SelectMany(gr => gr)
                .Select(inf => inf.Vertex.Location)
                .Distinct());
        }
    }
}
