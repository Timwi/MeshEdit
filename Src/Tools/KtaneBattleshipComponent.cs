using System.Collections.Generic;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("KTANE Battleship Component")]
        public static void KtaneBattleshipComponent()
        {
            var size = 5;

            var x1 = -.7;
            var x2 = .3;
            var y1 = -.7;
            var y2 = .3;

            var oDepth = .15;
            var iDepth = .14;

            var padding = .03;
            var spacing = .02;

            var w = (x2 - x1 - 2 * padding - (size - 1) * spacing) / size;
            var h = (y2 - y1 - 2 * padding - (size - 1) * spacing) / size;

            var newFaces = new List<Face>();
            for (int x = 0; x < size; x++)
            {
                var ix1 = x1 + padding + (w + spacing) * x;
                var ix2 = ix1 + w;

                for (int y = 0; y < size; y++)
                {
                    var iy1 = y1 + padding + (h + spacing) * y;
                    var iy2 = iy1 + h;

                    var ox1 = x == 0 ? x1 : ix1 - spacing / 2;
                    var oy1 = y == 0 ? y1 : iy1 - spacing / 2;
                    var ox2 = x == size - 1 ? x2 : ix2 + spacing / 2;
                    var oy2 = y == size - 1 ? y2 : iy2 + spacing / 2;

                    newFaces.Add(new Face(new[] { new Pt(ox2, oDepth, oy1), new Pt(ox1, oDepth, oy1), new Pt(ix1, iDepth, iy1), new Pt(ix2, iDepth, iy1) }));
                    newFaces.Add(new Face(new[] { new Pt(ox2, oDepth, oy2), new Pt(ox2, oDepth, oy1), new Pt(ix2, iDepth, iy1), new Pt(ix2, iDepth, iy2) }));
                    newFaces.Add(new Face(new[] { new Pt(ox2, oDepth, oy2), new Pt(ix2, iDepth, iy2), new Pt(ix1, iDepth, iy2), new Pt(ox1, oDepth, oy2) }));
                    newFaces.Add(new Face(new[] { new Pt(ix1, iDepth, iy1), new Pt(ox1, oDepth, oy1), new Pt(ox1, oDepth, oy2), new Pt(ix1, iDepth, iy2) }));
                }
            }
            
            Program.Settings.Execute(new AddRemoveFaces(new Face[0], newFaces
                .Select(face => new Face(face.Vertices.Select((v, i) => new VertexInfo(v.Location, null,
                    (face.Vertices[(i + 1) % face.Vertices.Length].Location - v.Location) *
                    (face.Vertices[(i + face.Vertices.Length - 1) % face.Vertices.Length].Location - v.Location)
                )).ToArray()))
                .ToArray()
            ));
        }
    }
}
