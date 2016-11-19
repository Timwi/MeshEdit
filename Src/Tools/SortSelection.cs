using System;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Sort Selection")]
        public static void SortSelection(
            [ToolBool(
                "• Which direction?",
                "Clockwise",
                "Counter-clockwise")]
            bool cw)
        {
            var midPt = new Pt(
                Program.Settings.SelectedVertices.Sum(p => p.X) / Program.Settings.SelectedVertices.Count,
                Program.Settings.SelectedVertices.Sum(p => p.Y) / Program.Settings.SelectedVertices.Count,
                Program.Settings.SelectedVertices.Sum(p => p.Z) / Program.Settings.SelectedVertices.Count);

            Program.Settings.SelectedVertices.Sort((p1, p2) =>
            {
                var atan1 = Math.Atan2(p1.Z - midPt.Z, p1.X - midPt.X);
                var atan2 = Math.Atan2(p2.Z - midPt.Z, p2.X - midPt.X);
                return atan1.CompareTo(atan2) * (cw ? -1 : 1);
            });
        }
    }
}
