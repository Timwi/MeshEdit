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
    sealed class Settings : SettingsBase, IClassifyObjectProcessor
    {
        public string Filename = null;
        public string LastDir = null;

        [ClassifyNotNull]
        public List<Pt> SelectedVertices = new List<Pt>();
        [ClassifyNotNull]
        public List<Face> SelectedFaces = new List<Face>();
        private bool _isFaceSelected;
        public bool IsFaceSelected { get { return _isFaceSelected && SelectedFaces.Count > 0; } }
        public bool IsAnythingSelected { get { return _isFaceSelected ? SelectedFaces.Count > 0 : SelectedVertices.Count > 0; } }

        [ClassifyNotNull]
        public List<Face> Faces = new List<Face>();
        public string ObjectName;

        [ClassifyNotNull]
        public ManagedForm.Settings MainWindowSettings = new ManagedForm.Settings();
        [ClassifyNotNull]
        public ManagedForm.Settings ToolWindowSettings = new ManagedForm.Settings();

        [ClassifyNotNull]
        public Stack<UndoItem> Undo = new Stack<UndoItem>();
        [ClassifyNotNull]
        public Stack<UndoItem> Redo = new Stack<UndoItem>();

        // Each object is either a Pt[] or a Face[]
        [ClassifyNotNull]
        public object[] RememberedSelections = new object[10];

        public RectangleD? ShowingRect;

        public bool ShowNormals;
        public bool ShowTextures;

        public event Action UpdateUI;

        public void SelectFace(int? index)
        {
            SelectedVertices.Clear();
            SelectedFaces.Clear();
            if (index != null && index.Value >= 0 && index.Value < Faces.Count)
                SelectedFaces.Add(Faces[index.Value]);
            _isFaceSelected = SelectedFaces.Count > 0;
            UpdateUI?.Invoke();
        }

        public void SelectFace(Face face)
        {
            SelectedFaces.Clear();
            if (face != null && Faces.Contains(face))
                SelectedFaces.Add(face);
            SelectedVertices.Clear();
            _isFaceSelected = SelectedFaces.Count > 0;
            UpdateUI?.Invoke();
        }

        public void SelectFaces(IEnumerable<Face> Faces)
        {
            SelectedFaces = Faces.ToList();
            SelectedVertices.Clear();
            _isFaceSelected = SelectedFaces.Count > 0;
            UpdateUI?.Invoke();
        }

        public void SelectVertex(Pt? vertex)
        {
            SelectedFaces.Clear();
            SelectedVertices.Clear();
            if (vertex != null)
                SelectedVertices.Add(vertex.Value);
            _isFaceSelected = false;
            UpdateUI?.Invoke();
        }

        public void SelectVertices(IEnumerable<Pt> vertices)
        {
            SelectedVertices.Clear();
            if (vertices != null)
                SelectedVertices.AddRange(vertices.Distinct());
            SelectedFaces.Clear();
            _isFaceSelected = false;
            UpdateUI?.Invoke();
        }

        public void Deselect()
        {
            SelectedFaces.Clear();
            SelectedVertices.Clear();
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

        void IClassifyObjectProcessor.BeforeSerialize() { }
        void IClassifyObjectProcessor.AfterDeserialize()
        {
            SelectedFaces = SelectedFaces.Intersect(Faces).ToList();
            SelectedVertices = SelectedVertices.Where(v => Faces.Any(f => f.Locations.Contains(v))).ToList();
            _isFaceSelected = SelectedFaces.Count > 0;
        }
    }
}
