using System;
using System.Linq;
using RT.Util.Geometry;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Recalculate texture coordinates from bounds")]
        public static void RecalculateTextures([ToolDouble("Multiply Y coordinate with factor?")] double yFactor)
        {
            if (Program.Settings.Faces.Count == 0)
                return;

            PointD translateCoords(Pt p) => new PointD(p.X + yFactor * p.Y, p.Z + yFactor * p.Y);

            var minX = Program.Settings.Faces.Min(f => f.Vertices.Min(v => translateCoords(v.Location).X));
            var minY = Program.Settings.Faces.Min(f => f.Vertices.Min(v => translateCoords(v.Location).Y));
            var maxX = Program.Settings.Faces.Max(f => f.Vertices.Max(v => translateCoords(v.Location).X));
            var maxY = Program.Settings.Faces.Max(f => f.Vertices.Max(v => translateCoords(v.Location).Y));
            Program.Settings.Execute(new ModifyTextureCoordinates(
                Program.Settings.Faces
                    .Where(f => !f.Hidden)
                    .SelectMany(f => f.Vertices)
                    .Select(v =>
                    {
                        var nv = translateCoords(v.Location);
                        return Tuple.Create(v, v.Texture, new PointD((nv.X - minX) / (maxX - minX), (nv.Y - maxY) / (minY - maxY)));
                    })
                    .ToArray()));
        }
    }
}
