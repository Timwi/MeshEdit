using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Generate beveled inset")]
        public static void GenerateInset([ToolDouble("Bevel radius?")] double radius)
        {
            if (Program.Settings.SelectedVertices.Count < 3)
            {
                DlgMessage.ShowInfo(@"Need at least 3 vertices for this tool.");
                return;
            }

            Program.Settings.Execute(new AddRemoveFaces(null, bevelFromCurve(Program.Settings.SelectedVertices, radius, 12).Select(f => new Face(f, false)).ToArray()));
        }

        private static IEnumerable<Pt[]> bevelFromCurve(List<Pt> pts, double radius, int revSteps)
        {
            return createMesh(true, false, pts
                .Select((p, ix) => new
                {
                    AxisStart = p.Add(y: -radius),
                    AxisEnd = p.Add(y: -radius) + (pts[(ix + 1) % pts.Count] - p) + (p - pts[(ix - 1 + pts.Count) % pts.Count]),
                    Perpendicular = pts[ix]
                })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => -90 * i / (revSteps - 1))
                    .Select(angle => inf.Perpendicular.Rotate(inf.AxisStart, inf.AxisEnd, angle))
                    .ToArray())
                .ToArray());
        }

        private static IEnumerable<Pt[]> createMesh(bool closedX, bool closedY, Pt[][] pts)
        {
            return Enumerable.Range(0, pts.Length)
                .SelectConsecutivePairs(closedX, (i1, i2) => Enumerable.Range(0, pts[0].Length)
                    .SelectConsecutivePairs(closedY, (j1, j2) => new[] { pts[i1][j1], pts[i2][j1], pts[i2][j2], pts[i1][j2] }))
                .SelectMany(x => x);
        }
    }
}
