using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Generate a circle inside a face")]
        public static void GenerateCircle([ToolDouble("Radius of the circle?")] double radius, [ToolInt("Number of points?")] int steps)
        {
            if (!Program.Settings.IsFaceSelected || Program.Settings.SelectedFaceIndex == null)
            {
                DlgMessage.ShowInfo("Need a selected face for this tool.");
                return;
            }
            var face = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value];

            const double centerX = 0;
            const double centerZ = .1;

            double centerY = face.Vertices.Aggregate(0d, (prev, next) => prev + next.Location.Y) / face.Vertices.Length;

            var newFaces = Enumerable.Range(0, steps)
                .Select(k => 360.0 * k / steps)
                .Select(angle => new Pt(radius * cs(angle) + centerX, centerY, radius * sn(angle) + centerZ))
                .Select(pt => new { Point = pt, Closest = face.Vertices.MinElement(v => v.Location.Distance(pt)) })
                .SelectConsecutivePairs(true, (i1, i2) => new Face(
                    (i1.Closest == i2.Closest ? new[] { i1.Point, i2.Point, i1.Closest.Location } : new[] { i1.Point, i2.Point, i2.Closest.Location, i1.Closest.Location })
                        .Select(v => new VertexInfo(v, null, new Pt(0, 1, 0)))
                        .ToArray()));

            Program.Settings.Execute(new AddRemoveFaces(new[] { face }, newFaces.ToArray()));
        }
    }
}
