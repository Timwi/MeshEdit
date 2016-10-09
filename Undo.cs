using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshEdit
{
    abstract class UndoItem
    {
        public abstract void Undo();
        public abstract void Redo();
    }

    sealed class MoveVertex : UndoItem
    {
        public Pt Before { get; private set; }
        public Pt After { get; private set; }
        public KeyValuePair<Face, int[]>[] Affected { get; private set; }
        public MoveVertex(Pt before, Pt after, KeyValuePair<Face, int[]>[] affected)
        {
            Before = before;
            After = after;
            Affected = affected;
        }
        private MoveVertex() { } // Classify

        public override void Undo()
        {
            foreach (var inf in Affected)
                foreach (var ix in inf.Value)
                    inf.Key.Vertices[ix] = Before;
            Program.Settings.SelectVertex(Before);
        }

        public override void Redo()
        {
            foreach (var inf in Affected)
                foreach (var ix in inf.Value)
                    inf.Key.Vertices[ix] = After;
            Program.Settings.SelectVertex(After);
        }
    }

    abstract class FullUndoItem : UndoItem
    {
        public List<Face> FacesBefore;
        public List<Face> FacesAfter;

        public override void Undo()
        {
            Program.Settings.Faces = FacesBefore;
        }

        public override void Redo()
        {
            Program.Settings.Faces = FacesAfter;
        }
    }

    sealed class DeleteFace : UndoItem
    {
        public Face Face;
        public int Index;

        public DeleteFace(int index)
        {
            Face = Program.Settings.Faces[index];
            Index = index;
        }
        private DeleteFace() { } // Classify

        public override void Redo()
        {
            Program.Settings.Faces.RemoveAt(Index);
            Program.Settings.SelectFace(null);
        }

        public override void Undo()
        {
            Program.Settings.Faces.Insert(Index, Face);
            Program.Settings.SelectFace(Index);
        }
    }
}
