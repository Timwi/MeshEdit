using System.Linq;
using RT.Util.ExtensionMethods;
using RT.Util.Forms;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Generate beveled inset")]
        public static void GenerateInset(
            [ToolDouble("Bevel radius?")] double radius,
            [ToolBool("Open or closed curve?", "&Open", "&Closed")] bool closed,
            [ToolDouble("Extra wall depth?")] double extraWallDepth,
            [ToolBool("Miter join calculation?", "&Normalized", "N&on-normalized")] bool nonNormalizedMiter,
            [ToolInt("Number of steps of revolution?")] int revSteps)
        {
            if (Program.Settings.SelectedVertices.Count < (closed ? 3 : 2))
            {
                DlgMessage.ShowInfo(@"Need at least {0} vertices for {1} curve.".Fmt(closed ? 3 : 2, closed ? "a closed" : "an open"));
                return;
            }

            var pts = Program.Settings.SelectedVertices;

            Pt mn(Pt p) => nonNormalizedMiter ? p : p.Normalize();

            var nPts = pts
                .Select((p, ix) => new
                {
                    AxisStart = closed || ix != pts.Count - 1 ? p.Add(y: -radius) : pts[pts.Count - 2].Add(y: -radius),
                    AxisEnd = closed || (ix != 0 && ix != pts.Count - 1) ? p.Add(y: -radius) + mn(pts[(ix + 1) % pts.Count] - p) + mn(p - pts[(ix - 1 + pts.Count) % pts.Count]) :
                        ix == 0 ? pts[1].Add(y: -radius) : pts[pts.Count - 1].Add(y: -radius),
                    Perpendicular = pts[ix],
                    Center = pts[ix].Add(y: -radius)
                })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => -90 * i / (revSteps - 1))
                    .Select(angle => new { inf.Center, Rotated = inf.Perpendicular.Rotate(inf.AxisStart, inf.AxisEnd, angle) })
                    .Concat(new { Center = inf.Center.Add(y: -extraWallDepth), Rotated = inf.Perpendicular.Rotate(inf.AxisStart, inf.AxisEnd, -90).Add(y: -extraWallDepth) })
                    .ToArray())
                .ToArray();

            var faces = Enumerable.Range(0, nPts.Length)
                .SelectConsecutivePairs(closed, (i1, i2) => Enumerable.Range(0, nPts[0].Length)
                    .SelectConsecutivePairs(false, (j1, j2) => new[] { nPts[i1][j1], nPts[i2][j1], nPts[i2][j2], nPts[i1][j2] }.Select(inf => new VertexInfo(inf.Rotated, null, inf.Rotated - inf.Center)).ToArray()))
                .SelectMany(x => x);

            Program.Settings.Execute(new AddRemoveFaces(null, faces.Select(f => new Face(f, false)).ToArray()));
        }
    }
}
