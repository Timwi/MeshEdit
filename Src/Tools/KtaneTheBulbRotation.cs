using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RT.Util.Dialogs;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("KTANE The Bulb rotation")]
        public static void KtaneTheBulbRotation()
        {
            var axisEnd = new Pt(-0.42426406871192862, 0.150511, -0.32426406871192848);
            var axisStart = new Pt(0.4242640687119284, 0.150511, -0.32426406871192859);
            var tilt = 30;  // angle in degrees

            Program.Settings.Execute(new MoveVertices(
                Program.Settings.SelectedVertices
                    .Select(p => Tuple.Create(p, p.Rotate(axisStart, axisEnd, tilt)))
                    .ToArray()));
        }
    }
}
