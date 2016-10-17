using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Geometry;

namespace MeshEdit.Tools
{
    sealed class TextForKtaneFollowTheLeader : Tool
    {
        public override string Name => "Text (KTANE, Follow The Leader)";
        public static readonly TextForKtaneFollowTheLeader Instance = new TextForKtaneFollowTheLeader();
        private TextForKtaneFollowTheLeader() { }

        static double sin(double θ) => Math.Sin(θ / 180 * Math.PI);
        static double cos(double θ) => Math.Cos(θ / 180 * Math.PI);

        private static double pow(double a, double b) => Math.Pow(a, b);

        private static IEnumerable<Pt> bézier(Pt start, Pt control1, Pt control2, Pt end, int steps)
        {
            return Enumerable.Range(0, steps)
                .Select(i => (double) i / (steps - 1))
                .Select(t => pow(1 - t, 3) * start + 3 * pow(1 - t, 2) * t * control1 + 3 * (1 - t) * t * t * control2 + pow(t, 3) * end);
        }

        private static IEnumerable<PointD> bézier(PointD start, PointD control1, PointD control2, PointD end, int steps)
        {
            return Enumerable.Range(0, steps)
                .Select(i => (double) i / (steps - 1))
                .Select(t => pow(1 - t, 3) * start + 3 * pow(1 - t, 2) * t * control1 + 3 * (1 - t) * t * t * control2 + pow(t, 3) * end);
        }

        public override void Execute()
        {
            var font = new Font("Gill Sans MT", 14f, FontStyle.Regular);
            var sizeFactor = .006;
            var baseY = 0.150511;
            var depth = .02;
            var bevelSize = .01;

            var newFaces = new List<Face>();

            for (int num = 0; num < 12; num++)
            {
                // Create a GraphicsPath from the number.
                GraphicsPath p;
                using (var bmp = new Bitmap(1024, 768, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    p = new GraphicsPath();
                    var angle = 240 + 30 * num;
                    var radius = num % 2 == 0 ? 130 : 35;
                    p.AddString((num + 1).ToString(), font.FontFamily, (int) font.Style, font.Size * g.DpiY / 72, new PointD(radius * cos(angle), radius * sin(angle)).ToPointF(), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }

                // Split the GraphicsPath up into each closed curve.
                var rawPts = new List<List<PointD>>();
                var rawTypes = new List<List<PathPointType>>();
                for (int i = 0; i < p.PointCount; i++)
                {
                    if ((PathPointType) p.PathTypes[i] == PathPointType.Start)
                    {
                        rawPts.Add(new List<PointD>());
                        rawTypes.Add(new List<PathPointType>());
                    }

                    if (rawPts.Last().Count == 0 || rawPts.Last().Last() != new PointD(p.PathPoints[i]))
                    {
                        rawPts.Last().Add(new PointD(p.PathPoints[i]));
                        rawTypes.Last().Add((PathPointType) p.PathTypes[i]);
                    }
                }

                // Filter out duplicate points at the end of each list
                for (int ix = 0; ix < rawPts.Count; ix++)
                {
                    while (rawPts[ix].Count > 1 && rawPts[ix].Last() == rawPts[ix].First())
                    {
                        rawPts[ix].RemoveAt(rawPts[ix].Count - 1);
                        rawTypes[ix].RemoveAt(rawTypes[ix].Count - 1);
                    }
                }

                // Recalculate the Bézier points in each curve.
                for (int i = 0; i < rawPts.Count; i++)
                {
                    var ps = rawPts[i];
                    var ts = rawTypes[i];
                    var j = 0;
                    while (j < ps.Count - 3)
                    {
                        if (ts[j + 1].HasFlag(PathPointType.Bezier) && ts[j + 2].HasFlag(PathPointType.Bezier) && ts[j + 3].HasFlag(PathPointType.Bezier))
                        {
                            var res = bézier(ps[j], ps[j + 1], ps[j + 2], ps[j + 3], 4).ToArray();
                            ps[j + 1] = res[1];
                            ps[j + 2] = res[2];
                            j += 3;
                        }
                        else
                            j++;
                    }
                }

                for (int ix = 0; ix < rawPts.Count; ix++)
                {
                    var pts = Enumerable.Range(0, rawPts[ix].Count)
                        .Select(i =>
                        {
                            var pd = rawPts[ix][i] * sizeFactor;
                            var prevPd = rawPts[ix][(i - 1 + rawPts[ix].Count) % rawPts[ix].Count] * sizeFactor;
                            var nextPd = rawPts[ix][(i + 1) % rawPts[ix].Count] * sizeFactor;
                            var befV = pd - prevPd;
                            var afterV = nextPd - pd;

                            var p1 = prevPd + rot(befV).Unit() * bevelSize;
                            var p2 = pd + rot(afterV).Unit() * bevelSize;
                            var outerP = Intersect.LineWithLine(new EdgeD(p1, p1 + befV), new EdgeD(p2, p2 + afterV));
                            if (double.IsNaN(outerP.X))
                                outerP = pd + rot(afterV).Unit() * bevelSize;

                            return new
                            {
                                InnerPoint = new Pt(pd.X, baseY + depth, pd.Y),
                                OuterPoint = new Pt(outerP.X, baseY, outerP.Y),
                                Type = (PathPointType) rawTypes[ix][i]
                            };
                        })
                        .ToArray();

                    newFaces.AddRange(pts.SelectConsecutivePairs(true, (p1, p2) =>
                    {
                        return new Face(Ut.NewArray(
                            new VertexInfo(p2.OuterPoint, null, new Pt(0, 1, 0)),
                            new VertexInfo(p1.OuterPoint, null, new Pt(0, 1, 0)),
                            new VertexInfo(p1.InnerPoint, null, p1.Type.HasFlag(PathPointType.Bezier) ? (p1.OuterPoint - p1.InnerPoint).Set(y: 0) : -(p2.InnerPoint - p1.InnerPoint).RotateY(90)),
                            new VertexInfo(p2.InnerPoint, null, p2.Type.HasFlag(PathPointType.Bezier) ? (p2.OuterPoint - p2.InnerPoint).Set(y: 0) : -(p2.InnerPoint - p1.InnerPoint).RotateY(90))
                        ));
                    }).Where(f => f != null));
                }
            }
            Program.Settings.Execute(new AddRemoveFaces(null, newFaces.ToArray()));
        }

        private PointD rot(PointD p) { return new PointD(p.Y, -p.X); }
    }
}
