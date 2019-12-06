using System;
using System.Linq;
using RT.Util.ExtensionMethods;

namespace MeshEdit
{
    static partial class Tools
    {
        public enum SelectionMode
        {
            SelectAll,
            SelectAllNonHidden,
            Intersect
        }

        [Tool("Select from Y coordinate")]
        public static void SelectFromY([ToolDouble("Y coordinate?")] double y, [ToolDouble("Tolerance?")] double tolerance, [ToolEnum("Select what?", typeof(SelectionMode), "All matching vertices", "Non-hidden faces only", "Intersect with current selection")] SelectionMode mode)
        {
            var matches = Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => Math.Abs(v.Location.Y - y) <= tolerance).Select(v => v.Location)).Distinct();

            switch (mode)
            {
                case SelectionMode.SelectAll:
                    Program.Settings.SelectedVertices.Clear();
                    Program.Settings.SelectedVertices.AddRange(Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => Math.Abs(v.Location.Y - y) <= tolerance).Select(v => v.Location)).Distinct());
                    break;
                case SelectionMode.SelectAllNonHidden:
                    Program.Settings.SelectedVertices.Clear();
                    Program.Settings.SelectedVertices.AddRange(Program.Settings.Faces.Where(f => !f.Hidden).SelectMany(f => f.Vertices.Where(v => Math.Abs(v.Location.Y - y) <= tolerance).Select(v => v.Location)).Distinct());
                    break;
                case SelectionMode.Intersect:
                    Program.Settings.SelectedVertices.RemoveAll(v => !matches.Contains(v));
                    break;
            }
        }
    }
}
