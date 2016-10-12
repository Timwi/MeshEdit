using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;
using RT.Util.Forms;
using RT.Util.Geometry;

namespace MeshEdit
{
    public partial class Mainform : ManagedForm
    {
        private const int _paddingX = 20;
        private const int _paddingY = 20;
        private static SizeF _selectionSize = new SizeF(10, 10);

        private double _minX;
        private double _minY;
        private double _boundingW;
        private double _boundingH;
        private RectangleD _displayRect;

        // When using “F” to switch between face selection and vertex selection,
        // remembers the vertex so that we select a different face each time.
        private int? _lastFaceFromVertexIndex;
        private Pt? _lastFaceFromVertex;

        // Alt+Arrow keys
        private Pt? _selectingFromVertex;
        private int? _selectingDirection;
        private bool _selectingDirectionFixed;
        private Pt[] _selectingFrom;
        private int _selectingFromIndex;

        // Drag & drop
        private bool _isMouseDown = false;
        private Pt _beforeMouse;
        private Pt _afterMouse;
        private VertexInfo[] _mouseAffected;

        private Bitmap _background = null;

        // Oemplus key in vertex mode
        private Pt? _selectedForEdgeSplit = null;
        // OemMinus key in vertex mode
        private Pt? _selectedForFaceSplit = null;
        // Oemplus key in face mode
        private int? _selectedForFaceMerge = null;

        public Mainform() : base(Program.Settings.MainWindowSettings)
        {
            if (Program.Settings.SelectedFaceIndex != null && Program.Settings.SelectedFaceIndex >= Program.Settings.Faces.Count)
                Program.Settings.SelectedFaceIndex = null;
            if (Program.Settings.SelectedVertex != null && !Program.Settings.Faces.Any(f => f.Locations.Contains(Program.Settings.SelectedVertex.Value)))
                Program.Settings.SelectedVertex = null;

            if (Program.Settings.BackgroundFilename != null && File.Exists(Program.Settings.BackgroundFilename))
                _background = new Bitmap(Program.Settings.BackgroundFilename);

            recalculateBounds();
            InitializeComponent();
        }

        private void openFile()
        {
            var vertices = new List<Pt>();
            var textures = new List<PointD>();
            var normals = new List<Pt>();
            var faces = new List<Tuple<int, int, int>[]>();

            foreach (var line in File.ReadAllLines(Program.Settings.Filename))
            {
                Match m;
                if ((m = Regex.Match(line, @"^v (-?\d*\.?\d+) (-?\d*\.?\d+) (-?\d*\.?\d+)$")).Success)
                    vertices.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if ((m = Regex.Match(line, @"^vt (-?\d*\.?\d+) (-?\d*\.?\d+)$")).Success)
                    textures.Add(new PointD(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value)));
                else if ((m = Regex.Match(line, @"^vn (-?\d*\.?\d+) (-?\d*\.?\d+) (-?\d*\.?\d+)$")).Success)
                    normals.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if (line.StartsWith("f "))
                    faces.Add(line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s =>
                        {
                            var ixs = s.Split(new[] { '/' });
                            var vix = int.Parse(ixs[0]) - 1;
                            var tix = ixs.Length > 1 ? int.Parse(ixs[1]) - 1 : -1;
                            var nix = ixs.Length > 2 ? int.Parse(ixs[2]) - 1 : -1;
                            return Tuple.Create(vix, tix, nix);
                        }).ToArray());
            }

            Program.Settings.Faces = faces.Select(f => new Face(f.Select(ix => new VertexInfo(vertices[ix.Item1], textures[ix.Item2], normals[ix.Item3])).ToArray())).ToList();
        }

        private void recalculateBounds()
        {
            _minX = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.X));
            _minY = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.Z));
            _boundingW = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.X)) - _minX;
            _boundingH = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.Z)) - _minY;
            _displayRect = fitIntoMaintainAspectRatio(_boundingW, _boundingH, new RectangleD(_paddingX, _paddingY, ClientSize.Width - 2 * _paddingX, ClientSize.Height - 2 * _paddingY));
        }

        private Tuple<Face, int[]>[] getAffected(Pt p)
        {
            return Program.Settings.Faces.Select(f => Tuple.Create(f, f.Locations.SelectIndexWhere(v => v == p).ToArray())).Where(inf => inf.Item2.Length > 0).ToArray();
        }

        void mouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left) && Program.Settings.Faces.Count > 0)
            {
                Program.Settings.SelectedVertex = _beforeMouse = _afterMouse = Program.Settings.Faces.SelectMany(f => f.Locations).MinElement(v => trP(v).Apply(p => Math.Sqrt(Math.Pow(p.X - e.X, 2) + Math.Pow(p.Y - e.Y, 2))));
                _mouseAffected = Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => v.Location == _beforeMouse)).ToArray();
                _isMouseDown = true;
                mainPanel.Refresh();
            }
        }

        void mouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                _afterMouse = revTrP(e.X, e.Y, Program.Settings.SelectedVertex.Value);
                foreach (var v in _mouseAffected)
                    v.Location = _afterMouse;
                Program.Settings.SelectedVertex = _afterMouse;
                mainPanel.Refresh();
            }
        }

        private void mouseUp(object sender, MouseEventArgs e)
        {
            _isMouseDown = false;
            if (_beforeMouse != _afterMouse)
            {
                Program.Settings.Undo.Push(new MoveVertex(_beforeMouse, _afterMouse, _mouseAffected));
                recalculateBounds();
                mainPanel.Refresh();
                save();
            }
        }

        private void save()
        {
            //var allFaces = Program.Settings.Faces;

            var vs = Program.Settings.Faces.SelectMany(f => f.Locations).Distinct().ToList();
            var ts = Program.Settings.Faces.SelectMany(f => f.Textures).Distinct().ToList();
            var ns = Program.Settings.Faces.SelectMany(f => f.Normals).Distinct().ToList();
            var txtVs = vs.Select(p => $"v {p.X} {p.Y} {p.Z}");
            var txtTs = ts.Select(t => $"vt {t.X} {t.Y}");
            var txtNs = ns.Select(n => $"vn {n.X} {n.Y} {n.Z}");
            var txtFs = Program.Settings.Faces.Select(f => $"f {f.Vertices.Select(vi => $"{vs.IndexOf(vi.Location) + 1}/{ts.IndexOf(vi.Texture) + 1}/{ns.IndexOf(vi.Normal) + 1}").JoinString(" ")}");
            File.WriteAllLines(Program.Settings.Filename, txtVs.Concat(txtTs).Concat(txtNs).Concat(txtFs));
            Program.Settings.Save(onFailure: SettingsOnFailure.ShowRetryWithCancel);
        }

        private void keyDown(object sender, KeyEventArgs e)
        {
            var shift = e.KeyData.HasFlag(Keys.Shift);
            var ctrl = e.KeyData.HasFlag(Keys.Control);
            var alt = e.KeyData.HasFlag(Keys.Alt);
            var combo = (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + e.KeyCode.ToString();
            bool anyChanges = true;

            switch (combo)
            {
                case "F":
                    if (Program.Settings.IsFaceSelected)
                    {
                        Program.Settings.IsFaceSelected = false;
                        if (Program.Settings.SelectedFaceIndex != null && Program.Settings.SelectedFaceIndex == _lastFaceFromVertexIndex &&
                            _lastFaceFromVertex != null && Program.Settings.Faces[_lastFaceFromVertexIndex.Value].Locations.Contains(_lastFaceFromVertex.Value))
                            Program.Settings.SelectedVertex = _lastFaceFromVertex;
                        else if (Program.Settings.SelectedFaceIndex != null)
                            Program.Settings.SelectedVertex = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value].Locations.FirstOrNull();
                        else
                            Program.Settings.SelectedVertex = null;
                    }
                    else
                    {
                        Program.Settings.IsFaceSelected = true;
                        if (Program.Settings.SelectedVertex != null)
                        {
                            if (_lastFaceFromVertex != Program.Settings.SelectedVertex)
                            {
                                _lastFaceFromVertex = Program.Settings.SelectedVertex.Value;
                                _lastFaceFromVertexIndex = -1;
                            }
                            _lastFaceFromVertexIndex =
                                Enumerable.Range(_lastFaceFromVertexIndex.Value + 1, Program.Settings.Faces.Count - _lastFaceFromVertexIndex.Value - 1).Where(i => Program.Settings.Faces[i].Locations.Contains(_lastFaceFromVertex.Value)).FirstOrNull() ??
                                Enumerable.Range(0, _lastFaceFromVertexIndex.Value + 1).Where(i => Program.Settings.Faces[i].Locations.Contains(_lastFaceFromVertex.Value)).FirstOrNull();
                            Program.Settings.SelectFace(_lastFaceFromVertexIndex);
                        }
                    }
                    break;

                case "Ctrl+Z": case "Alt+Back": undo(); break;
                case "Ctrl+Y": case "Alt+Shift+Back": redo(); break;

                case "Ctrl+O":
                    using (var dlg = new OpenFileDialog { DefaultExt = "obj", Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*" })
                    {
                        if (Program.Settings.LastDir != null)
                            dlg.InitialDirectory = Program.Settings.LastDir;
                        var result = dlg.ShowDialog();
                        if (result == DialogResult.Cancel)
                            break;
                        Program.Settings.Filename = dlg.FileName;
                        Program.Settings.LastDir = dlg.InitialDirectory;
                        openFile();
                        Program.Settings.Undo.Clear();
                        Program.Settings.Redo.Clear();
                        Program.Settings.SelectedVertex = null;
                        Program.Settings.SelectedFaceIndex = null;
                        Program.Settings.IsFaceSelected = false;
                    }
                    break;

                case "Ctrl+B":
                    using (var dlg = new OpenFileDialog { Filter = "Graphics files (*.png; *.jpg; *.jpeg; *.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*" })
                    {
                        if (Program.Settings.LastBackgroundDir != null || Program.Settings.LastDir != null)
                            dlg.InitialDirectory = Program.Settings.LastBackgroundDir ?? Program.Settings.LastDir;
                        var result = dlg.ShowDialog();
                        if (result == DialogResult.Cancel)
                            break;
                        Program.Settings.BackgroundFilename = dlg.FileName;
                        Program.Settings.LastBackgroundDir = dlg.InitialDirectory;
                        _background = new Bitmap(dlg.FileName);
                    }
                    break;

                case "Ctrl+S":
                    save();
                    break;

                case "T":
                    execute(new ChangeTextures(Program.Settings.Faces
                        .SelectMany(f => f.Vertices.Select(v => new { Face = f, Vertex = v }))
                        .GroupBy(inf => inf.Vertex.Location)
                        .Where(gr => gr.All(inf => !inf.Face.Hidden))
                        .SelectMany(gr => gr)
                        .Select(inf => inf.Vertex)
                        .Distinct()
                        .Select(v => Tuple.Create(v, v.Texture, new PointD(.4771284794 * v.Location.X + .46155, -.4771284794 * v.Location.Z + .5337373145)))
                        .ToArray()));
                    break;

                default:
                    if (Program.Settings.IsFaceSelected)
                    {
                        if (Program.Settings.SelectedFaceIndex != null)
                        {
                            switch (combo)
                            {
                                case "Tab":
                                case "Shift+Tab":
                                    Program.Settings.SelectedFaceIndex = (Program.Settings.SelectedFaceIndex.Value + Program.Settings.Faces.Count + (shift ? -1 : 1)) % Program.Settings.Faces.Count;
                                    break;

                                case "H":
                                    var face = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value];
                                    execute(new SetHidden(new[] { face }, !face.Hidden));
                                    break;

                                case "Ctrl+H":
                                    execute(new SetHidden(Program.Settings.Faces.ToArray(), true));
                                    break;

                                case "Ctrl+Shift+H":
                                    execute(new SetHidden(Program.Settings.Faces.ToArray(), false));
                                    break;

                                case "Delete":
                                    if (Program.Settings.SelectedFaceIndex != null)
                                        execute(new DeleteFace(Program.Settings.SelectedFaceIndex.Value));
                                    break;

                                case "Oemplus":
                                    if (Program.Settings.SelectedFaceIndex == null)
                                        break;

                                    if (_selectedForFaceMerge == null)
                                        _selectedForFaceMerge = Program.Settings.SelectedFaceIndex;
                                    else
                                    {
                                        var face1 = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value];
                                        var face2 = Program.Settings.Faces[_selectedForFaceMerge.Value];
                                        var commonLocs = face1.Locations.Intersect(face2.Locations).ToArray();
                                        var newVertices = new List<VertexInfo>();
                                        var curFace = face1;
                                        var startVix = face1.Vertices.IndexOf(v => !commonLocs.Contains(v.Location));
                                        Ut.Assert(startVix != -1);
                                        var vix = startVix;
                                        do
                                        {
                                            var loc = curFace.Vertices[vix].Location;
                                            if (commonLocs.Contains(loc))
                                            {
                                                vix = face1.Locations.IndexOf(loc);
                                                var vix2 = face2.Locations.IndexOf(loc);
                                                Ut.Assert(vix != -1);
                                                Ut.Assert(vix2 != -1);

                                                // Generate the averaged vertex
                                                newVertices.Add(new VertexInfo(
                                                    loc,
                                                    (face1.Vertices[vix].Texture + face2.Vertices[vix2].Texture) / 2,
                                                    (face1.Vertices[vix].Normal + face2.Vertices[vix2].Normal) / 2));

                                                // Switch faces
                                                if (curFace == face1)
                                                {
                                                    curFace = face2;
                                                    vix = vix2;
                                                }
                                                else
                                                    curFace = face1;
                                            }
                                            else
                                                newVertices.Add(curFace.Vertices[vix]);
                                            vix = (vix + 1) % curFace.Vertices.Length;
                                        }
                                        while (curFace != face1 || vix != startVix);
                                        execute(new MergeFaces(face1, face2, new Face(newVertices.ToArray(), face1.Hidden && face2.Hidden)));
                                        _selectedForFaceMerge = null;
                                    }
                                    break;

                                default:
                                    anyChanges = false;
                                    break;
                            }
                        }
                        else if (combo == "Tab" && Program.Settings.Faces.Count > 0)
                            Program.Settings.SelectedFaceIndex = 0;
                        else if (combo == "Shift+Tab" && Program.Settings.Faces.Count > 0)
                            Program.Settings.SelectedFaceIndex = Program.Settings.Faces.Count - 1;
                        else
                            anyChanges = false;
                    }
                    else
                    {
                        if (Program.Settings.SelectedVertex != null)
                        {
                            int? moveX = null;
                            int moveY = 0;

                            var processArrow = Ut.Lambda((int direction) =>
                            {
                                if (_selectingFromVertex == null)
                                {
                                    _selectingFromVertex = Program.Settings.SelectedVertex;
                                    _selectingDirection = null;
                                    _selectingDirectionFixed = false;
                                    _selectingFrom = null;
                                }

                                if (!_selectingDirectionFixed)
                                {
                                    if (_selectingDirection == null)
                                        _selectingDirection = direction;
                                    else if (_selectingDirection.Value % 2 == 0 && Math.Abs(_selectingDirection.Value - direction) == 2)
                                        _selectingDirection = (_selectingDirection.Value + direction) / 2;
                                    else if (_selectingDirection.Value % 2 == 0 && Math.Abs(_selectingDirection.Value - direction) == 6)
                                        _selectingDirection = 7;
                                    var sel = _selectingFromVertex.Value;
                                    _selectingFrom = Program.Settings.Faces.Where(f => !f.Hidden || shift).SelectMany(f => f.Locations)
                                        .Where(v => (int) (Math.Atan2(v.Z - sel.Z, v.X - sel.X) / Math.PI * 4 + 8.5) % 8 == _selectingDirection.Value)
                                        .Concat(sel)
                                        .Distinct()
                                        .OrderBy(v => (sel.X - v.X) * (sel.X - v.X) + (sel.Z - v.Z) * (sel.Z - v.Z))
                                        .ToArray();
                                    _selectingFromIndex = _selectingFrom.Length == 0 ? 0 : 1;
                                }

                                if ((Math.Abs(direction - _selectingDirection.Value) <= 1 || Math.Abs(direction - _selectingDirection.Value) >= 7))
                                {
                                    if (_selectingFromIndex < _selectingFrom.Length)
                                        Program.Settings.SelectedVertex = _selectingFrom[_selectingFromIndex++];
                                }
                                else if (Math.Abs(direction - _selectingDirection.Value) != 2 && Math.Abs(direction - _selectingDirection.Value) != 6 && _selectingFromIndex >= 2)
                                    Program.Settings.SelectedVertex = _selectingFrom[--_selectingFromIndex - 1];
                            });

                            switch (combo)
                            {
                                case "C":
                                    Program.Settings.SelectedVertex?.Apply(v => { Clipboard.SetText($"({v.X:R}, {v.Y:R}, {v.Z:R})"); });
                                    break;

                                case "H":
                                case "Shift+H":
                                    if (Program.Settings.SelectedVertex != null)
                                        execute(new SetHidden(Program.Settings.Faces.Where(f => f.Locations.Contains(Program.Settings.SelectedVertex.Value)).ToArray(), !shift));
                                    break;

                                case "Left":
                                case "Ctrl+Left":
                                    moveX = ctrl ? -5 : -1; break;
                                case "Right":
                                case "Ctrl+Right":
                                    moveX = ctrl ? 5 : 1; break;
                                case "Up":
                                case "Ctrl+Up":
                                    moveX = 0; moveY = ctrl ? -5 : -1; break;
                                case "Down":
                                case "Ctrl+Down":
                                    moveX = 0; moveY = ctrl ? 5 : 1; break;

                                case "X": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertex.Value.X)); anyChanges = false; break;
                                case "Y": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertex.Value.Y)); anyChanges = false; break;
                                case "Z": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertex.Value.Z)); anyChanges = false; break;
                                case "Shift+X": replaceVertex(Program.Settings.SelectedVertex.Value.Set(x: ExactConvert.ToDouble(Clipboard.GetText()))); break;
                                case "Shift+Y": replaceVertex(Program.Settings.SelectedVertex.Value.Set(y: ExactConvert.ToDouble(Clipboard.GetText()))); break;
                                case "Shift+Z": replaceVertex(Program.Settings.SelectedVertex.Value.Set(z: ExactConvert.ToDouble(Clipboard.GetText()))); break;

                                case "Alt+Right": case "Alt+Shift+Right": processArrow(0); break;
                                case "Alt+Down": case "Alt+Shift+Down": processArrow(2); break;
                                case "Alt+Left": case "Alt+Shift+Left": processArrow(4); break;
                                case "Alt+Up": case "Alt+Shift+Up": processArrow(6); break;

                                case "Oemplus":
                                    if (Program.Settings.SelectedVertex == null)
                                        break;

                                    if (_selectedForEdgeSplit == null)
                                        _selectedForEdgeSplit = Program.Settings.SelectedVertex;
                                    else
                                    {
                                        var sel = Program.Settings.SelectedVertex.Value;
                                        var affectedFaces = Program.Settings.Faces
                                            .Select(f => Tuple.Create(f, Enumerable.Range(0, f.Vertices.Length)
                                                .Where(i =>
                                                    (f.Vertices[i].Location == _selectedForEdgeSplit.Value && f.Vertices[(i + 1) % f.Vertices.Length].Location == sel) ||
                                                    (f.Vertices[i].Location == sel && f.Vertices[(i + 1) % f.Vertices.Length].Location == _selectedForEdgeSplit.Value))
                                                .ToArray()))
                                            .Where(tup => tup.Item2.Length > 0)
                                            .ToArray();
                                        execute(new CreateVertex(affectedFaces));
                                        _selectedForEdgeSplit = null;
                                    }
                                    break;

                                case "Delete":
                                    if (Program.Settings.SelectedVertex == null)
                                        break;
                                    execute(new DeleteVertex(getAffected(Program.Settings.SelectedVertex.Value)));
                                    break;

                                case "OemMinus":
                                    if (Program.Settings.SelectedVertex == null)
                                        break;

                                    if (_selectedForFaceSplit == null)
                                        _selectedForFaceSplit = Program.Settings.SelectedVertex;
                                    else
                                    {
                                        var sel = Program.Settings.SelectedVertex.Value;
                                        var affectedFace = Program.Settings.Faces
                                            .Select(f => new { Face = f, Index1 = f.Vertices.IndexOf(v => v.Location == sel), Index2 = f.Vertices.IndexOf(v => v.Location == _selectedForFaceSplit.Value) })
                                            .Where(inf => inf.Index1 != -1 && inf.Index2 != -1)
                                            .FirstOrDefault();
                                        if (affectedFace != null)
                                            execute(new SplitFace(affectedFace.Face, affectedFace.Index1, affectedFace.Index2));
                                        _selectedForFaceSplit = null;
                                    }
                                    break;

                                default:
                                    anyChanges = false;
                                    break;
                            }

                            if (moveX != null)
                            {
                                var before = Program.Settings.SelectedVertex.Value;
                                var point = trP(before);
                                var after = revTrP(point.X + moveX.Value, point.Y + moveY, before);
                                execute(new MoveVertex(before, after, Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => v.Location == before)).ToArray()));
                            }
                        }
                        else
                            anyChanges = false;
                    }
                    break;
            }

            if (anyChanges)
            {
                mainPanel.Refresh();
                save();
            }
        }

        private void keyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                case Keys.Right:
                case Keys.Down:
                    _selectingDirectionFixed = true;
                    break;

                case Keys.Menu:
                    _selectingFromVertex = null;
                    mainPanel.Refresh();
                    break;
            }
        }

        private void replaceVertex(Pt newVertex)
        {
            if (Program.Settings.SelectedVertex == null)
                return;
            execute(new MoveVertex(
                Program.Settings.SelectedVertex.Value,
                newVertex,
                Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => v.Location == Program.Settings.SelectedVertex)).ToArray()));
        }

        private static void execute(UndoItem ui)
        {
            ui.Redo();
            Program.Settings.Redo.Clear();
            Program.Settings.Undo.Push(ui);
        }

        private static RectangleD fitIntoMaintainAspectRatio(double fitWidth, double fitHeight, RectangleD fitInto)
        {
            double x, y, w, h;

            if (fitWidth / fitHeight > fitInto.Width / fitInto.Height)
            {
                w = fitInto.Width;
                x = fitInto.Left;
                h = fitHeight / fitWidth * fitInto.Width;
                y = fitInto.Top + fitInto.Height / 2 - h / 2;
            }
            else
            {
                h = fitInto.Height;
                y = fitInto.Top;
                w = fitWidth / fitHeight * fitInto.Height;
                x = fitInto.Left + fitInto.Width / 2 - w / 2;
            }

            return new RectangleD(x, y, w, h);
        }

        void paint(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.High;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.Clear(Color.White);

            var usedVertices = new HashSet<VertexInfo>();
            foreach (var inf in Program.Settings.Faces.Select((f, i) => new { Face = f, Index = i }).OrderBy(x => !x.Face.Hidden))
            {
                var poly = inf.Face.Locations.Select(v => trP(v).ToPointF()).ToArray();
                e.Graphics.FillPolygon(
                    Program.Settings.IsFaceSelected && inf.Index == Program.Settings.SelectedFaceIndex
                        ? (inf.Face.Hidden ? Brushes.PaleVioletRed : Brushes.MediumVioletRed)
                        : (inf.Face.Hidden ? Brushes.LightSalmon : Brushes.LightGray),
                    poly);
                e.Graphics.DrawPolygon(new Pen(Brushes.DarkGray, 2f) { LineJoin = LineJoin.Round }, poly);
                if (!inf.Face.Hidden)
                    usedVertices.AddRange(inf.Face.Vertices);
            }

            using (var f = new Font("Agency FB", 10f, FontStyle.Regular))
            {
                foreach (var gr in usedVertices.GroupBy(v => v.Location))
                {
                    var dp = trP(gr.Key).ToPointF();
                    e.Graphics.DrawString($"({gr.Key.X:0.######}, {gr.Key.Y:0.######}, {gr.Key.Z:0.######})", f, Brushes.Black, dp, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                    var i = 0;
                    foreach (var t in gr.Select(v => v.Texture).Distinct())
                        e.Graphics.DrawString($"({t.X:0.######}, {t.Y:0.######})", f, Brushes.CadetBlue, dp + new SizeF(0, 15 * (++i)), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                }
            }

            if (!Program.Settings.IsFaceSelected && Program.Settings.SelectedVertex != null)
                e.Graphics.DrawEllipse(new Pen(Color.Navy, 2f), new RectangleF(trP(Program.Settings.SelectedVertex.Value).ToPointF() - new SizeF(_selectionSize.Width / 2, _selectionSize.Height / 2), _selectionSize));

            using (var f = new Font("Impact", 12f, FontStyle.Bold))
            {
                if (_selectedForEdgeSplit != null)
                    e.Graphics.DrawString("+", f, Brushes.Red, trP(_selectedForEdgeSplit.Value).ToPointF(), new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center });
                if (_selectedForFaceSplit != null)
                    e.Graphics.DrawString("−", f, Brushes.Red, trP(_selectedForFaceSplit.Value).ToPointF(), new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center });
            }

            if (_selectingFromVertex != null && _selectingDirection != null && _selectingFrom != null)
            {
                var orig = trP(_selectingFromVertex.Value).ToPointF();
                var size = Math.Max(ClientSize.Width, ClientSize.Height) * 2;
                e.Graphics.FillPolygon(new SolidBrush(Color.FromArgb(64, 220, 0, 0)), Ut.NewArray(
                    orig,
                    orig + new SizeF(
                        (float) (size * Math.Cos((45 * _selectingDirection.Value - 22.5) / 180 * Math.PI)),
                        (float) (size * Math.Sin((45 * _selectingDirection.Value - 22.5) / 180 * Math.PI))),
                    orig + new SizeF(
                        (float) (size * Math.Cos((45 * _selectingDirection.Value + 22.5) / 180 * Math.PI)),
                        (float) (size * Math.Sin((45 * _selectingDirection.Value + 22.5) / 180 * Math.PI)))
                ));

                foreach (var v in _selectingFrom)
                    if (v != Program.Settings.SelectedVertex)
                        e.Graphics.DrawEllipse(new Pen(Color.CornflowerBlue, 2f), new RectangleF(trP(v).ToPointF() - new SizeF(_selectionSize.Width / 2, _selectionSize.Height / 2), _selectionSize));

                e.Graphics.DrawLine(new Pen(Brushes.Navy, 7.5f) { EndCap = LineCap.ArrowAnchor }, orig, trP(Program.Settings.SelectedVertex.Value).ToPointF());
            }
        }

        private PointD trP(Pt p) => new PointD(
            (p.X - _minX) / _boundingW * _displayRect.Width + _paddingX,
            (p.Z - _minY) / _boundingH * _displayRect.Height + _paddingY);

        private Pt revTrP(double screenX, double screenY, Pt? orig = null) => new Pt(
            (screenX - _paddingX) / _displayRect.Width * _boundingW + _minX,
            orig?.Y ?? 0,
            (screenY - _paddingY) / _displayRect.Height * _boundingH + _minY);

        private void resize(object sender, EventArgs e)
        {
            recalculateBounds();
            mainPanel.Refresh();
        }

        private void undo()
        {
            if (Program.Settings.Undo.Count == 0)
                return;
            var item = Program.Settings.Undo.Pop();
            Program.Settings.Redo.Push(item);
            item.Undo();
        }

        private void redo()
        {
            if (Program.Settings.Redo.Count == 0)
                return;
            var item = Program.Settings.Redo.Pop();
            Program.Settings.Undo.Push(item);
            item.Redo();
        }
    }
}
