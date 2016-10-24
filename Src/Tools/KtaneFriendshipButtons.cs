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
        [Tool("KTANE Friendship — submit button")]
        public static void KtaneFriendshipSubmitButton()
        {
            if (!Program.Settings.IsFaceSelected || Program.Settings.SelectedFaceIndex == null)
            {
                DlgMessage.ShowInfo("Need a selected face for this tool.");
                return;
            }
            var face = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value];

            const double centerX = -0.64;
            const double centerZ = 0.3;
            const double innerRadius = .16; // .2 × .8
            const double outerRadius = .18;
            const double innerY = .13;
            const double outerY = .150511;
            const int steps = 72;

            var newFaces = new List<Face>();

            foreach (var inf in Enumerable.Range(0, steps).Select(k => 360.0 * k / steps).Select(angle => new
            {
                Inner = new Pt(innerRadius * cs(angle) + centerX, innerY, innerRadius * sn(angle) + centerZ),
                Outer = new Pt(outerRadius * cs(angle) + centerX, outerY, outerRadius * sn(angle) + centerZ)
            }).ConsecutivePairs(true))
            {
                var i1 = inf.Item1;
                var i2 = inf.Item2;

                // Bevel
                newFaces.Add(new Face(Ut.NewArray(
                    new VertexInfo(i2.Inner, null, new Pt(centerX, innerY, centerZ) - i2.Inner),
                    new VertexInfo(i2.Outer, null, new Pt(0, 1, 0)),
                    new VertexInfo(i1.Outer, null, new Pt(0, 1, 0)),
                    new VertexInfo(i1.Inner, null, new Pt(centerX, innerY, centerZ) - i1.Inner))));

                // Rim
                var closest1 = face.Vertices.MinElement(v => v.Location.Distance(i1.Outer));
                var closest2 = face.Vertices.MinElement(v => v.Location.Distance(i2.Outer));
                newFaces.Add(new Face(
                    (closest1 == closest2 ? new[] { i1.Outer, i2.Outer, closest1.Location } : new[] { i1.Outer, i2.Outer, closest2.Location, closest1.Location })
                    .Select(v => new VertexInfo(v, null, new Pt(0, 1, 0)))
                    .ToArray()));
            }

            Program.Settings.Execute(new AddRemoveFaces(new[] { face }, newFaces.ToArray()));
        }

        [Tool("KTANE Friendship — up/down buttons")]
        public static void KtaneFriendshipUpDownButtons()
        {
            if (Program.Settings.SelectedVertices.Count != 12)
            {
                DlgMessage.ShowInfo("Need 12 vertices for this tool.");
                return;
            }

            var outerDn = Program.Settings.SelectedVertices.Skip(0).Take(3).Select(pt => Program.Settings.Faces.SelectMany(fc => fc.Vertices).Where(v => v.Location == pt).ToArray()).ToArray();
            var innerDn = Program.Settings.SelectedVertices.Skip(3).Take(3).Select(pt => Program.Settings.Faces.SelectMany(fc => fc.Vertices).Where(v => v.Location == pt).ToArray()).ToArray();
            var outerUp = Program.Settings.SelectedVertices.Skip(6).Take(3).Select(pt => Program.Settings.Faces.SelectMany(fc => fc.Vertices).Where(v => v.Location == pt).ToArray()).ToArray();
            var innerUp = Program.Settings.SelectedVertices.Skip(9).Take(3).Select(pt => Program.Settings.Faces.SelectMany(fc => fc.Vertices).Where(v => v.Location == pt).ToArray()).ToArray();

            var undo = new List<Tuple<VertexInfo, Pt, Pt, Pt?, Pt?>>();

            const double f = .8 * .1;
            const double innerY = .13;
            const double outerY = .150511;
            const double normalFactor = 20;

            for (int k = 0; k < 3; k++)
            {
                // Down
                var innerDnX = (.12 * f * cs(360 * k / 3 + 90) - .052) * 10;
                var innerDnZ = (.12 * f * sn(360 * k / 3 + 90) + .06) * 10;

                var outerDnX = (.16 * f * cs(360 * k / 3 + 90) - .052) * 10;
                var outerDnZ = (.16 * f * sn(360 * k / 3 + 90) + .06) * 10;

                // Up
                var innerUpX = (-.12 * f * cs(360 * k / 3 + 90) - .07) * 10;
                var innerUpZ = (-.12 * f * sn(360 * k / 3 + 90) + .0648) * 10;

                var outerUpX = (-.16 * f * cs(360 * k / 3 + 90) - .07) * 10;
                var outerUpZ = (-.16 * f * sn(360 * k / 3 + 90) + .0648) * 10;

                foreach (var v in outerDn[k])
                    undo.Add(Tuple.Create(v, v.Location, new Pt(outerDnX, outerY, outerDnZ), v.Normal, new Pt(0, 1, 0).Nullable()));
                foreach (var v in innerDn[k])
                    undo.Add(Tuple.Create(v, v.Location, new Pt(innerDnX, innerY, innerDnZ), v.Normal, v.Normal));
                foreach (var v in outerUp[k])
                    undo.Add(Tuple.Create(v, v.Location, new Pt(outerUpX, outerY, outerUpZ), v.Normal, new Pt(0, 1, 0).Nullable()));
                foreach (var v in innerUp[k])
                    undo.Add(Tuple.Create(v, v.Location, new Pt(innerUpX, innerY, innerUpZ), v.Normal, v.Normal));
            }

            Program.Settings.Execute(new MoveVertices(undo.ToArray()));
        }

        [Tool("KTANE component — set vertical normals")]
        public static void KtaneSetVerticalNormals()
        {
            Program.Settings.Execute(new MoveVertices(Program.Settings.Faces.SelectMany(f => f.Vertices)
                .Where(v => v.Location.Y == .150511)
                .Select(v => new Tuple<VertexInfo, Pt, Pt, Pt?, Pt?>(v, v.Location, v.Location, v.Normal, new Pt(0, 1, 0)))
                .ToArray()));
        }

        static double cs(double a) => Math.Cos(a / 180 * Math.PI);
        static double sn(double a) => Math.Sin(a / 180 * Math.PI);
    }
}
