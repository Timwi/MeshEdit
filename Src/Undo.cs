using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util.ExtensionMethods;
using RT.Util.Geometry;

namespace MeshEdit
{
    abstract class UndoItem
    {
        public abstract void Undo();
        public abstract void Redo();
    }

    sealed class MoveVertex : UndoItem
    {
        Pt _before, _after;
        VertexInfo[] _affected;

        public MoveVertex(Pt before, Pt after, VertexInfo[] affected)
        {
            _before = before;
            _after = after;
            _affected = affected;
        }
        private MoveVertex() { } // Classify

        public override void Undo()
        {
            foreach (var inf in _affected)
                inf.Location = _before;
            Program.Settings.SelectVertex(_before);
        }

        public override void Redo()
        {
            foreach (var inf in _affected)
                inf.Location = _after;
            Program.Settings.SelectVertex(_after);
        }
    }

    abstract class FullUndoItem : UndoItem
    {
        List<Face> _facesBefore;
        List<Face> _facesAfter;

        public override void Undo()
        {
            Program.Settings.Faces = _facesBefore;
        }

        public override void Redo()
        {
            Program.Settings.Faces = _facesAfter;
        }
    }

    sealed class DeleteFace : UndoItem
    {
        Face _face;
        int _index;

        public DeleteFace(int index)
        {
            _face = Program.Settings.Faces[index];
            _index = index;
        }
        private DeleteFace() { } // Classify

        public override void Redo()
        {
            Program.Settings.Faces.RemoveAt(_index);
            Program.Settings.SelectFace(null);
        }

        public override void Undo()
        {
            Program.Settings.Faces.Insert(_index, _face);
            Program.Settings.SelectFace(_index);
        }
    }

    sealed class CreateVertex : UndoItem
    {
        Tuple<Face, int[]>[] _affectedFaces;

        public CreateVertex(Tuple<Face, int[]>[] affectedFaces)
        {
            _affectedFaces = affectedFaces;
        }

        private CreateVertex() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _affectedFaces)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var index in tup.Item2.OrderBy(ix => ix))
                    list.RemoveAt(index + 1);
                tup.Item1.Vertices = list.ToArray();
            }
        }

        public override void Redo()
        {
            foreach (var tup in _affectedFaces)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var index in tup.Item2.OrderByDescending(ix => ix))
                    list.Insert(index + 1, new VertexInfo(
                        (list[index].Location + list[(index + 1) % list.Count].Location) / 2,
                        (list[index].Texture + list[(index + 1) % list.Count].Texture) / 2,
                        (list[index].Normal + list[(index + 1) % list.Count].Normal) / 2));
                tup.Item1.Vertices = list.ToArray();
            }
        }
    }

    sealed class DeleteVertex : UndoItem
    {
        Tuple<Face, Tuple<int, VertexInfo>[]>[] _affected;

        public DeleteVertex(Tuple<Face, int[]>[] affected)
        {
            _affected = affected.Select(af => Tuple.Create(af.Item1, af.Item2.Select(ix => Tuple.Create(ix, af.Item1.Vertices[ix])).ToArray())).ToArray();
        }

        private DeleteVertex() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _affected)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var inner in tup.Item2.OrderBy(x => x.Item1))
                    list.Insert(inner.Item1, inner.Item2);
                tup.Item1.Vertices = list.ToArray();
            }
        }

        public override void Redo()
        {
            foreach (var tup in _affected)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var inner in tup.Item2.OrderByDescending(x => x.Item1))
                    list.RemoveAt(inner.Item1);
                tup.Item1.Vertices = list.ToArray();
            }
        }
    }

    sealed class SplitFace : UndoItem
    {
        Face _oldFace;
        Face _newFace1;
        Face _newFace2;

        public SplitFace(Face face, int index1, int index2)
        {
            _oldFace = face;
            if (index1 > index2)
            {
                var t = index1;
                index1 = index2;
                index2 = t;
            }
            _newFace1 = new Face(face.Vertices.Subarray(index1, index2 - index1 + 1), face.Hidden);
            _newFace2 = new Face(face.Vertices.Subarray(index2).Concat(face.Vertices.Subarray(0, index1 + 1)).ToArray(), face.Hidden);
        }

        private SplitFace() { } // Classify

        public override void Undo()
        {
            Program.Settings.Faces.Remove(_newFace1);
            Program.Settings.Faces.Remove(_newFace2);
            Program.Settings.Faces.Add(_oldFace);
            Program.Settings.IsFaceSelected = true;
            Program.Settings.SelectedFaceIndex = Program.Settings.Faces.Count - 1;
        }

        public override void Redo()
        {
            Program.Settings.Faces.Remove(_oldFace);
            Program.Settings.Faces.Add(_newFace1);
            Program.Settings.Faces.Add(_newFace2);
            Program.Settings.IsFaceSelected = true;
            Program.Settings.SelectedFaceIndex = Program.Settings.Faces.Count - 1;
        }
    }

    sealed class MergeFaces : UndoItem
    {
        Face _oldFace1;
        Face _oldFace2;
        Face _newFace;

        public MergeFaces(Face face1, Face face2, Face newFace)
        {
            _oldFace1 = face1;
            _oldFace2 = face2;
            _newFace = newFace;
        }

        private MergeFaces() { } // Classify

        public override void Undo()
        {
            Program.Settings.Faces.Remove(_newFace);
            Program.Settings.Faces.Add(_oldFace1);
            Program.Settings.Faces.Add(_oldFace2);
            Program.Settings.IsFaceSelected = true;
            Program.Settings.SelectedFaceIndex = Program.Settings.Faces.Count - 1;
        }

        public override void Redo()
        {
            Program.Settings.Faces.Remove(_oldFace1);
            Program.Settings.Faces.Remove(_oldFace2);
            Program.Settings.Faces.Add(_newFace);
            Program.Settings.IsFaceSelected = true;
            Program.Settings.SelectedFaceIndex = Program.Settings.Faces.Count - 1;
        }
    }

    sealed class SetHidden : UndoItem
    {
        Tuple<Face, bool>[] _faces;
        bool _setTo;

        public SetHidden(Face[] faces, bool setTo)
        {
            _faces = faces.Select(f => Tuple.Create(f, f.Hidden)).ToArray();
            _setTo = setTo;
        }

        private SetHidden() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _faces)
                tup.Item1.Hidden = tup.Item2;
        }

        public override void Redo()
        {
            foreach (var tup in _faces)
                tup.Item1.Hidden = _setTo;
        }
    }

    sealed class ChangeTextures : UndoItem
    {
        Tuple<VertexInfo, PointD, PointD>[] _data;

        public ChangeTextures(Tuple<VertexInfo, PointD, PointD>[] data) { _data = data; }
        private ChangeTextures() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _data)
                tup.Item1.Texture = tup.Item2;
        }

        public override void Redo()
        {
            foreach (var tup in _data)
                tup.Item1.Texture = tup.Item3;
        }
    }
}
