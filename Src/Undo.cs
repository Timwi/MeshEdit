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

    sealed class MoveVertices : UndoItem
    {
        // VertexInfo, old location, new location, old normal, new normal
        private Tuple<VertexInfo, Pt, Pt, Pt?, Pt?>[] _changes;

        public MoveVertices(Tuple<Pt, Pt>[] changes)
        {
            var list = new List<Tuple<VertexInfo, Pt, Pt, Pt?, Pt?>>();
            foreach (var vi in Program.Settings.Faces.SelectMany(f => f.Vertices))
            {
                var tup = changes.IndexOf(tp => tp.Item1 == vi.Location);
                if (tup == -1)
                    continue;
                list.Add(Tuple.Create(vi, vi.Location, changes[tup].Item2, vi.Normal, vi.Normal));
            }
            _changes = list.ToArray();
        }
        public MoveVertices(Tuple<VertexInfo, Pt, Pt>[] changes)
        {
            _changes = changes.Select(v => Tuple.Create(v.Item1, v.Item2, v.Item3, v.Item1.Normal, v.Item1.Normal)).ToArray();
        }
        public MoveVertices(Tuple<VertexInfo, Pt, Pt, Pt?, Pt?>[] changes)
        {
            _changes = changes;
        }
        private MoveVertices() { } // Classify

        public override void Undo()
        {
            foreach (var inf in _changes)
            {
                inf.Item1.Location = inf.Item2;
                inf.Item1.Normal = inf.Item4;
            }
            Program.Settings.SelectedVertices = _changes.Select(tup => tup.Item1.Location).Distinct().ToList();
        }

        public override void Redo()
        {
            foreach (var inf in _changes)
            {
                inf.Item1.Location = inf.Item3;
                inf.Item1.Normal = inf.Item5;
            }
            Program.Settings.SelectedVertices = _changes.Select(tup => tup.Item1.Location).Distinct().ToList();
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
            Program.Settings.SelectedVertices = _affectedFaces.SelectMany(tup => tup.Item2.Select(index => tup.Item1.Vertices[index].Location)).Distinct().ToList();
        }

        public override void Redo()
        {
            Pt location = default(Pt);
            foreach (var tup in _affectedFaces)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var index in tup.Item2.OrderByDescending(ix => ix))
                    list.Insert(index + 1, new VertexInfo(
                        (location = (list[index].Location + list[(index + 1) % list.Count].Location) / 2),
                        (list[index].Texture + list[(index + 1) % list.Count].Texture) / 2,
                        (list[index].Normal + list[(index + 1) % list.Count].Normal) / 2));
                tup.Item1.Vertices = list.ToArray();
            }
            Program.Settings.SelectedVertices = new List<Pt> { location };
        }
    }

    sealed class DeleteVertices : UndoItem
    {
        Tuple<Face, Tuple<int, VertexInfo>[]>[] _affected;
        Face[] _deleted;

        public DeleteVertices(Tuple<Face, int[]>[] affected, Face[] deleted)
        {
            _affected = affected.Select(af => Tuple.Create(af.Item1, af.Item2.Select(ix => Tuple.Create(ix, af.Item1.Vertices[ix])).ToArray())).ToArray();
            _deleted = deleted;
        }

        private DeleteVertices() { } // Classify

        public override void Undo()
        {
            foreach (var tup in _affected)
            {
                var list = tup.Item1.Vertices.ToList();
                foreach (var inner in tup.Item2.OrderBy(x => x.Item1))
                    list.Insert(inner.Item1, inner.Item2);
                tup.Item1.Vertices = list.ToArray();
            }
            Program.Settings.Faces.AddRange(_deleted);
            Program.Settings.SelectVertices(_affected.SelectMany(tup => tup.Item2.Select(tup2 => tup2.Item2.Location)).Distinct());
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
            Program.Settings.Faces.RemoveRange(_deleted);
            Program.Settings.SelectVertex(null);
        }
    }

    sealed class AddRemoveFaces : UndoItem
    {
        Face[] _oldFaces;
        Face[] _newFaces;

        public AddRemoveFaces(Face[] oldFaces, Face[] newFaces)
        {
            _oldFaces = oldFaces ?? new Face[0];
            _newFaces = newFaces ?? new Face[0];
        }

        private AddRemoveFaces() { } // Classify

        public override void Undo()
        {
            Program.Settings.Faces.RemoveRange(_newFaces);
            Program.Settings.Faces.AddRange(_oldFaces);
            Program.Settings.SelectFaces(_oldFaces);
        }

        public override void Redo()
        {
            Program.Settings.Faces.RemoveRange(_oldFaces);
            Program.Settings.Faces.AddRange(_newFaces);
            Program.Settings.SelectFaces(_newFaces);
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

    sealed class ReverseFaces : UndoItem
    {
        Face[] _faces;

        public ReverseFaces(Face[] faces)
        {
            _faces = faces;
        }

        private ReverseFaces() { } // Classify

        public override void Undo()
        {
            foreach (var face in _faces)
                face.Vertices.ReverseInplace();
        }

        public override void Redo()
        {
            foreach (var face in _faces)
                face.Vertices.ReverseInplace();
        }
    }
}
