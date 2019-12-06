using System.Linq;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;
using RT.Util.Geometry;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Triangulate a face")]
        public static void TriangulateFace()
        {
            if (!Program.Settings.IsFaceSelected || Program.Settings.SelectedFaces.Count != 1)
            {
                DlgMessage.ShowInfo("Need exactly one selected face to triangulate.");
                return;
            }
            var face = Program.Settings.SelectedFaces[0];
            if (face.Vertices.Any(v => v.Location.Y != face.Vertices[0].Location.Y))
            {
                DlgMessage.ShowInfo("Not all vertices have the same Y coordinate.");
                return;
            }
            var y = face.Vertices[0].Location.Y;

            var result = Triangulate.DelaunayConstrained(
                face.Vertices.Select(v => new PointD(v.Location.X, v.Location.Z)), 
                face.Vertices.Select(v => new PointD(v.Location.X, v.Location.Z)).SelectConsecutivePairs(closed: true, selector: (p1, p2) => new EdgeD(p1, p2)));

            var newFaces = result.Select(triangle => new Face(triangle.Vertices.Select(v => new Pt(v.X, y, v.Y)).ToArray())).ToArray();
            Program.Settings.Execute(new AddRemoveFaces(new[] { face }, newFaces));
        }
    }
}
