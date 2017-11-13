using System.Collections.Generic;
using System.Linq;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Generate beveled inset")]
        public static void GenerateInset([ToolDouble("Bevel radius?")] double radius, [ToolBool("Closed curve?", "Open", "Closed")] bool closed, [ToolDouble("Extra wall depth?")] double extraWallDepth)
        {
            if (Program.Settings.SelectedVertices.Count < (closed ? 3 : 2))
            {
                DlgMessage.ShowInfo(@"Need at least {0} vertices for {1} curve.".Fmt(closed ? 3 : 2, closed ? "a closed" : "an open"));
                return;
            }

            Program.Settings.Execute(new AddRemoveFaces(null, bevelFromCurve(Program.Settings.SelectedVertices, radius, extraWallDepth, 12, closed).Select(f => new Face(f, false)).ToArray()));
        }

        private static IEnumerable<VertexInfo[]> bevelFromCurve(List<Pt> pts, double radius, double extraWallDepth, int revSteps, bool closed)
        {
            var nPts = pts
                .Select((p, ix) => new
                {
                    AxisStart = closed || ix != pts.Count - 1 ? p.Add(y: -radius) : pts[pts.Count - 2].Add(y: -radius),
                    AxisEnd = closed || (ix != 0 && ix != pts.Count - 1) ? p.Add(y: -radius) + (pts[(ix + 1) % pts.Count] - p) + (p - pts[(ix - 1 + pts.Count) % pts.Count]) :
                        ix == 0 ? pts[1].Add(y: -radius) : pts[pts.Count - 1].Add(y: -radius),
                    Perpendicular = pts[ix],
                    Center = pts[ix].Add(y: -radius)
                })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => -90 * i / (revSteps - 1))
                    .Select(angle => new { Center = inf.Center, Rotated = inf.Perpendicular.Rotate(inf.AxisStart, inf.AxisEnd, angle) })
                    .Concat(new { Center = inf.Center.Add(y: -extraWallDepth), Rotated = inf.Perpendicular.Rotate(inf.AxisStart, inf.AxisEnd, -90).Add(y: -extraWallDepth) })
                    .ToArray())
                .ToArray();

            return Enumerable.Range(0, nPts.Length)
                .SelectConsecutivePairs(closed, (i1, i2) => Enumerable.Range(0, nPts[0].Length)
                    .SelectConsecutivePairs(false, (j1, j2) => new[] { nPts[i1][j1], nPts[i2][j1], nPts[i2][j2], nPts[i1][j2] }.Select(inf => new VertexInfo(inf.Rotated, null, inf.Rotated - inf.Center)).ToArray()))
                .SelectMany(x => x);
        }
    }
}
