using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util.Geometry;

namespace MeshEdit
{
    static partial class Tools
    {
        [Tool("KTANE Double-Oh set texture coordinates")]
        public static void KtaneDoubleOhTexture()
        {
            var changes = new List<Tuple<VertexInfo, PointD?, PointD>>();
            foreach (var vertex in Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Location.Y >= .04999 && v.Location.Y <= .05001))
            {
                var center = -vertex.Location.Z + .5 < vertex.Location.X
                    ? /* small, bottom-right */ new Pt(.5, .05, .45)
                    : /* large, top-left */ new Pt(-0.25, 0.15, 0.1);
                var centerTexture = new PointD(.4771284794 * center.X + .46155, -.4771284794 * center.Z + .5337373145);
                if (vertex.Texture != null)
                    changes.Add(new Tuple<VertexInfo, PointD?, PointD>(vertex, vertex.Texture, .9 * vertex.Texture.Value + .1 * centerTexture));
            }
            Program.Settings.Execute(new ModifyTextureCoordinates(changes.ToArray()));
        }
    }
}
