using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.Forms;
using RT.Util.Geometry;
using RT.Util.Serialization;

namespace MeshEdit
{
    [Settings("MeshEdit", SettingsKind.UserSpecific, SettingsSerializer.ClassifyJson)]
    sealed class Settings : SettingsBase
    {
        public string Filename = null;
        public string LastDir = null;

        [ClassifyNotNull]
        public List<Pt> SelectedVertices = new List<Pt>();
        public int? SelectedFaceIndex = null;
        private bool _isFaceSelected;
        public bool IsFaceSelected { get { return _isFaceSelected; } }

        [ClassifyNotNull]
        public List<Face> Faces = new List<Face>();

        [ClassifyNotNull]
        public ManagedForm.Settings MainWindowSettings = new ManagedForm.Settings();
        [ClassifyNotNull]
        public ManagedForm.Settings ToolWindowSettings = new ManagedForm.Settings();

        [ClassifyNotNull]
        public Stack<UndoItem> Undo = new Stack<UndoItem>();
        [ClassifyNotNull]
        public Stack<UndoItem> Redo = new Stack<UndoItem>();
        [ClassifyNotNull]
        public VertexInfo[][] RememberedSelections = new VertexInfo[10][];

        public RectangleD? ShowingRect;

        public event Action UpdateUI;

        public void SelectFace(int? index)
        {
            SelectedFaceIndex = index == null || index >= 0 && index < Faces.Count ? index : null;
            _isFaceSelected = true;
            UpdateUI?.Invoke();
        }

        public void SelectVertex(Pt? vertex)
        {
            SelectedVertices = new List<Pt>();
            if (vertex != null)
                SelectedVertices.Add(vertex.Value);
            _isFaceSelected = false;
            UpdateUI?.Invoke();
        }

        public void SelectVertices(IEnumerable<Pt> vertices)
        {
            SelectedVertices = vertices?.ToList() ?? new List<Pt>();
            _isFaceSelected = false;
            UpdateUI?.Invoke();
        }

        public void Execute(UndoItem ui)
        {
            ui.Redo();
            Redo.Clear();
            Undo.Push(ui);
            UpdateUI?.Invoke();
        }
    }
}
