using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;
using RT.Util.Geometry;

namespace MeshEdit.Tools
{
    sealed class GenerateInset : Tool
    {
        public override string Name => "Generate beveled inset";
        public static readonly GenerateInset Instance = new GenerateInset();
        private GenerateInset() { }

        private static Pt rotate(Pt point, Pt axisStart, Pt axisEnd, double angle)
        {
            var x = point.X;
            var y = point.Y;
            var z = point.Z;
            var a = axisStart.X;
            var b = axisStart.Y;
            var c = axisStart.Z;
            var u = axisEnd.X - a;
            var v = axisEnd.Y - b;
            var w = axisEnd.Z - c;
            var nf = Math.Sqrt(u * u + v * v + w * w);
            u /= nf;
            v /= nf;
            w /= nf;
            var θ = angle * Math.PI / 180;
            var cosθ = Math.Cos(θ);
            var sinθ = Math.Sin(θ);

            return new Pt(
                (a * (v * v + w * w) - u * (b * v + c * w - u * x - v * y - w * z)) * (1 - cosθ) + x * cosθ + (-c * v + b * w - w * y + v * z) * sinθ,
                (b * (u * u + w * w) - v * (a * u + c * w - u * x - v * y - w * z)) * (1 - cosθ) + y * cosθ + (c * u - a * w + w * x - u * z) * sinθ,
                (c * (u * u + v * v) - w * (a * u + b * v - u * x - v * y - w * z)) * (1 - cosθ) + z * cosθ + (-b * u + a * v - v * x + u * y) * sinθ);
        }

        public override void Execute()
        {
            if (Program.Settings.SelectedVertices.Count < 3)
            {
                DlgMessage.ShowInfo(@"Need at least 3 vertices for this tool.");
                return;
            }

            const double bevelSize = .02;
            const double backFaceDepth = .1;
            const double endAngle = 135;
            const double angleStep = 15;

            var midPoint = (Program.Settings.SelectedVertices.Aggregate((prev, next) => prev + next) / Program.Settings.SelectedVertices.Count).Add(y: -backFaceDepth);

            var foo = Program.Settings.SelectedVertices
                .SelectConsecutivePairs(true, (p1, p2) => new { Start = p1, End = p2 })
                .SelectConsecutivePairs(true, (e1, e2) =>
                {
                    var p1 = e1.Start;
                    var p2 = e1.End;    // or e2.Start, they are the same
                    var p3 = e2.End;

                    // Axis of rotation
                    var axStart = p1.Add(y: -bevelSize);
                    var axEnd = p2.Add(y: -bevelSize);
                    // Create bevel from e1
                    var bevel = Ut.Range(0, endAngle, angleStep).SelectConsecutivePairs(false, (angle1, angle2) => new Face(Ut.NewArray(
                        rotate(p1, axStart, axEnd, angle2),
                        rotate(p2, axStart, axEnd, angle2),
                        rotate(p2, axStart, axEnd, angle1),
                        rotate(p1, axStart, axEnd, angle1))));

                    var bottom1 = rotate(p1, axStart, axEnd, endAngle);
                    var bottom2 = rotate(p2, axStart, axEnd, endAngle);
                    var backSupport = new Face(new[] { bottom2, bottom1, bottom1.Set(y: midPoint.Y), bottom2.Set(y: midPoint.Y) });
                    var backFace = new Face(new[] { midPoint, bottom2.Set(y: midPoint.Y), bottom1.Set(y: midPoint.Y) });

                    // Do we need a corner piece?
                    var dirChange = -Math.Atan2((p2.X - p1.X) * (p3.Z - p2.Z) - (p3.X - p2.X) * (p2.Z - p1.Z), (p2.X - p1.X) * (p2.Z - p1.Z) + (p3.X - p2.X) * (p3.Z - p2.Z));
                    if (dirChange > 0)
                    {
                        var ax2Start = axEnd;
                        var ax2End = p3.Add(y: -bevelSize);
                        var vAxStart = p2;
                        var vAxEnd = p2.Add(y: 1);
                        var corner =
                            Ut.Range(0, endAngle, angleStep).SelectConsecutivePairs(false, (angle1, angle2) =>
                                Ut.Range(0, dirChange / Math.PI * 180, 10).SelectConsecutivePairs(false, (θ1, θ2) =>
                                    new Face(Ut.NewArray(
                                        rotate(rotate(p2, axStart, axEnd, angle2), vAxStart, vAxEnd, θ1),
                                        rotate(rotate(p2, axStart, axEnd, angle2), vAxStart, vAxEnd, θ2),
                                        rotate(rotate(p2, axStart, axEnd, angle1), vAxStart, vAxEnd, θ2),
                                        rotate(rotate(p2, axStart, axEnd, angle1), vAxStart, vAxEnd, θ1)))));

                        var backFaceParts =
                                Ut.Range(0, dirChange / Math.PI * 180, 10).SelectConsecutivePairs(false, (θ1, θ2) =>
                                    new Face(Ut.NewArray(
                                        midPoint,
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ2).Set(y: midPoint.Y),
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ1).Set(y: midPoint.Y))));

                        var backSupportParts =
                                Ut.Range(0, dirChange / Math.PI * 180, 10).SelectConsecutivePairs(false, (θ1, θ2) =>
                                    new Face(Ut.NewArray(
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ2),
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ1),
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ1).Set(y: midPoint.Y),
                                        rotate(rotate(p2, axStart, axEnd, endAngle), vAxStart, vAxEnd, θ2).Set(y: midPoint.Y))));

                        bevel = bevel.Concat(corner.SelectMany(x => x)).Concat(backFaceParts).Concat(backSupportParts);
                    }

                    return bevel.Concat(new[] { backFace, backSupport });
                })
                .SelectMany(x => x)
                .ToArray();
            Program.Settings.Execute(new AddRemoveFaces(null, foo));
        }
    }
}
