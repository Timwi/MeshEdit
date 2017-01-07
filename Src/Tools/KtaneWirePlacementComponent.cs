using System.Collections.Generic;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("KTANE Wire Placement Component")]
        public static void KtaneWirePlacementComponent()
        {
            var y = .15;

            var x1 = -0.82;
            var x2 = 0.5;
            var y1 = -0.5;
            var y2 = 0.81;

            var newFaces = new List<Face>();
            var size = .1;
            var pix2 = 0d;
            for (int col = 0; col < 5; col++)
            {
                var ix1 = col == 0 ? x1 : 0.304 * (col - 1) - 0.608 + size;
                var ix2 = col == 4 ? x2 : 0.304 * col - 0.608 - size;
                newFaces.Add(new Face(new[] { new Pt(ix1, y, y1), new Pt(ix1, y, y2), new Pt(ix2, y, y2), new Pt(ix2, y, y1) }));

                if (col > 0)
                    for (int row = 0; row < 5; row++)
                    {
                        var iy1 = row == 0 ? y1 : 0.304 * (row - 1) - 0.304 + size;
                        var iy2 = row == 4 ? y2 : 0.304 * row - 0.304 - size;
                        newFaces.Add(new Face(new[] { new Pt(pix2, y, iy2), new Pt(ix1, y, iy2), new Pt(ix1, y, iy1), new Pt(pix2, y, iy1) }));
                    }

                pix2 = ix2;
            }

            Program.Settings.Execute(new AddRemoveFaces(new Face[0], newFaces
                .Select(face => new Face(face.Vertices.Select((v, i) => new VertexInfo(v.Location, null, new Pt(0, 1, 0))).ToArray()))
                .ToArray()
            ));
        }
    }
}
