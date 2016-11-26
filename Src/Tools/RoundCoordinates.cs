using System;
using System.Linq;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("Round Coordinates")]
        public static void RoundCoordinates([ToolDouble("Round to what? (e.g. 0.01)")] double roundTo)
        {
            if (Program.Settings.SelectedVertices.Count < 1)
                return;

            Program.Settings.Execute(new MoveVertices(Program.Settings.SelectedVertices.Select(p => Tuple.Create(p, round(p / roundTo) * roundTo)).ToArray()));
        }

        private static Pt round(Pt input) => new Pt(Math.Round(input.X), Math.Round(input.Y), Math.Round(input.Z));
    }
}
