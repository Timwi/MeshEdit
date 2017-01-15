using System;
using System.Linq;
using RT.Util.Geometry;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Recalculate texture coordinates (KTANE component)")]
        public static void RecalculateTexturesKTANE()
        {
            Program.Settings.Execute(new ModifyTextureCoordinates(
                Program.Settings.Faces
                    .Where(f => !f.Hidden)
                    .SelectMany(f => f.Vertices)
                    //.Where(v => Program.Settings.Faces.Where(f => f.Locations.Contains(v.Location)).All(f => !f.Hidden))
                    .Select(v => Tuple.Create(v, v.Texture, new PointD(.4771284794 * v.Location.X + .46155, -.4771284794 * v.Location.Z + .5337373145)))
                    .ToArray()));
        }
    }
}
