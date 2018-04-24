using System;
using System.Linq;
using RT.Util.Geometry;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Recalculate texture coordinates from bounds")]
        public static void RecalculateTextures()
        {
            if (Program.Settings.Faces.Count == 0)
                return;
            var minX = Program.Settings.Faces.Min(f => f.Vertices.Min(v => v.Location.X));
            var minZ = Program.Settings.Faces.Min(f => f.Vertices.Min(v => v.Location.Z));
            var maxX = Program.Settings.Faces.Max(f => f.Vertices.Max(v => v.Location.X));
            var maxZ = Program.Settings.Faces.Max(f => f.Vertices.Max(v => v.Location.Z));
            Program.Settings.Execute(new ModifyTextureCoordinates(
                Program.Settings.Faces
                    .Where(f => !f.Hidden)
                    .SelectMany(f => f.Vertices)
                    .Select(v => Tuple.Create(v, v.Texture, new PointD((v.Location.X - minX) / (maxX - minX), (v.Location.Z - maxZ) / (minZ - maxZ))))
                    .ToArray()));
        }
    }
}
