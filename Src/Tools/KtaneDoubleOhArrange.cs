using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("KTANE Double-Oh arrange selected vertices")]
        public static void KtaneDoubleOhArrange([ToolDouble("Center X?")] double centerX, [ToolDouble("Center Z?")] double centerZ, [ToolDouble("Radius?")] double radius, [ToolDouble("Bézier factor?")] double f)
        {
            // big (4 buttons)
            //var center = new Pt(-0.25, 0.15, 0.1);
            //var radius = .6;
            //var f = .2;

            // small (submit button)
            //var center = new Pt(.5, .15, .45);
            //var radius = .275;
            //var f = .05;

            // custom
            var center = new Pt(centerX, .15, centerZ);

            var bézierSteps = 72 / 4 + 1;

            var b1 = bézier(center + new Pt(-radius, 0, 0), center + new Pt(-radius, 0, f), center + new Pt(-f, 0, radius), center + new Pt(0, 0, radius), bézierSteps);
            var b2 = bézier(center + new Pt(0, 0, radius), center + new Pt(f, 0, radius), center + new Pt(radius, 0, f), center + new Pt(radius, 0, 0), bézierSteps);
            var b3 = bézier(center + new Pt(radius, 0, 0), center + new Pt(radius, 0, -f), center + new Pt(f, 0, -radius), center + new Pt(0, 0, -radius), bézierSteps);
            var b4 = bézier(center + new Pt(0, 0, -radius), center + new Pt(-f, 0, -radius), center + new Pt(-radius, 0, -f), center + new Pt(-radius, 0, 0), bézierSteps);
            var bs = b1.Skip(1).Concat(b2.Skip(1)).Concat(b3.Skip(1)).Concat(b4.Skip(1)).ToArray();

            Program.Settings.Execute(new MoveVertices(Ut.NewArray(Program.Settings.SelectedVertices.Count,
                i => Tuple.Create(Program.Settings.SelectedVertices[i], bs[i]))));
        }

        private static double pow(double a, double b) => Math.Pow(a, b);

        private static IEnumerable<Pt> bézier(Pt start, Pt control1, Pt control2, Pt end, int steps)
        {
            return Enumerable.Range(0, steps)
                .Select(i => (double) i / (steps - 1))
                .Select(t => pow(1 - t, 3) * start + 3 * pow(1 - t, 2) * t * control1 + 3 * (1 - t) * t * t * control2 + pow(t, 3) * end);
        }
    }
}
