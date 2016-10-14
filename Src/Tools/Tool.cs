using System;
using System.Linq;
using MeshEdit.Tools;
using RT.Util.Dialogs;

namespace MeshEdit
{
    abstract class Tool
    {
        public static Tool[] AllTools = new Tool[] { RecalculateTexturesKTANE.Instance, GenerateInset.Instance, SelectNonHiddenVertices.Instance, RecalculateNormals.Instance };

        public abstract void Execute();
        public abstract string Name { get; }

        public override string ToString() => Name;
    }
}
