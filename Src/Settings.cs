using System.Collections.Generic;
using RT.Util;
using RT.Util.Forms;
using RT.Util.Serialization;

namespace MeshEdit
{
    [Settings("MeshEdit", SettingsKind.UserSpecific)]
    sealed class Settings : SettingsBase
    {
        public string Filename = null;
        public string LastDir = null;
        public Pt? SelectedVertex = null;
        public int? SelectedFaceIndex = null;
        public bool IsFaceSelected = false;

        [ClassifyNotNull]
        public List<Face> Faces = new List<Face>();

        [ClassifyNotNull]
        public ManagedForm.Settings MainWindowSettings = new ManagedForm.Settings();

        [ClassifyNotNull]
        public Stack<UndoItem> Undo = new Stack<UndoItem>();
        public Stack<UndoItem> Redo = new Stack<UndoItem>();

        public void SelectFace(int? index)
        {
            SelectedFaceIndex = index == null || index >= 0 && index < Faces.Count ? index : null;
            IsFaceSelected = true;
        }

        public void SelectVertex(Pt? vertex)
        {
            SelectedVertex = vertex;
            IsFaceSelected = false;
        }
    }
}
