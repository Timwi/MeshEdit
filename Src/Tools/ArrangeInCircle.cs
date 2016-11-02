using System;
using RT.Util;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Arrange selected vertices in a circle")]
        public static void ArrangeInCircle(
            [ToolDouble("Radius of the circle?")] double radius,
            [ToolDouble("Offset angle? (0 = first vertex is right of the center; vertices go clockwise)")] double offsetAngle)
        {
            var center = new Pt(0, 0.150511, 0.1);

            Program.Settings.Execute(new MoveVertices(Ut.NewArray(Program.Settings.SelectedVertices.Count, i => Tuple.Create(
                Program.Settings.SelectedVertices[i],
                new Pt(center.X + radius * cs(i * 360 / Program.Settings.SelectedVertices.Count + offsetAngle), center.Y, center.Z + radius * sn(i * 360 / Program.Settings.SelectedVertices.Count + offsetAngle))))));
        }
    }
}
