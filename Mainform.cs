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

        public Mainform() : base(Program.Settings.MainWindowSettings)
        {
            if (Program.Settings.SelectedFaceIndex != null && Program.Settings.SelectedFaceIndex >= Program.Settings.Faces.Count)
                Program.Settings.SelectedFaceIndex = null;
            if (Program.Settings.SelectedVertex != null && !Program.Settings.Faces.Any(f => f.Vertices.Contains(Program.Settings.SelectedVertex.Value)))
                Program.Settings.SelectedVertex = null;

            recalculateBounds();
            InitializeComponent();
        }

        private void openFile()
        {
            var vertices = new List<Pt>();
            var faces = new List<int[]>();

            foreach (var line in File.ReadAllLines(Program.Settings.Filename))
            {
                Match m;
                if ((m = Regex.Match(line, @"^v (-?\d*\.?\d+) (-?\d*\.?\d+) (-?\d*\.?\d+)$")).Success)
                    vertices.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if (line.StartsWith("f "))
                    faces.Add(line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s.Contains('/') ? s.Substring(0, s.IndexOf('/')) : s) - 1).ToArray());
            }

            Program.Settings.Faces = faces.Select(f => new Face(f.Select(ix => vertices[ix]).ToArray())).ToList();
        }

        private void load(object sender, EventArgs e)
        {
            Left = Screen.PrimaryScreen.WorkingArea.Width / 2 - Width / 2;
            Top = Screen.PrimaryScreen.WorkingArea.Height / 2 - Height / 2 - 100;
        }

        private void recalculateBounds()
        {
            _minX = Program.Settings.Faces.Min(f => f.Vertices.Min(p => p.X));
            _minY = Program.Settings.Faces.Min(f => f.Vertices.Min(p => p.Z));
            _boundingW = Program.Settings.Faces.Max(f => f.Vertices.Max(p => p.X)) - _minX;
            _boundingH = Program.Settings.Faces.Max(f => f.Vertices.Max(p => p.Z)) - _minY;
            _displayRect = fitIntoMaintainAspectRatio(_boundingW, _boundingH, new RectangleD(_paddingX, _paddingY, ClientSize.Width - 2 * _paddingX, ClientSize.Height - 2 * _paddingY));
        }

        private Pt _beforeMouse;
        private Pt _afterMouse;
        private KeyValuePair<Face, int[]>[] _mouseAffected;

        void mouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left))
            {
                _beforeMouse = Program.Settings.Faces.SelectMany(f => f.Vertices).MinElement(v => trP(v).Apply(p => Math.Sqrt(Math.Pow(p.X - e.X, 2) + Math.Pow(p.Y - e.Y, 2))));
                Program.Settings.SelectedVertex = _beforeMouse;
                _mouseAffected = getAffected(_beforeMouse);
                mainPanel.Refresh();
            }
        }

        private KeyValuePair<Face, int[]>[] getAffected(Pt p)
        {
            return Program.Settings.Faces.Select(f => Ut.KeyValuePair(f, f.Vertices.SelectIndexWhere(v => v == p).ToArray())).Where(inf => inf.Value.Length > 0).ToArray();
        }

        void mouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left) && Program.Settings.SelectedVertex != null)
            {
                _afterMouse = revTrP(e.X, e.Y, Program.Settings.SelectedVertex.Value);
                foreach (var inf in _mouseAffected)
                    foreach (var ix in inf.Value)
                        inf.Key.Vertices[ix] = _afterMouse;
                Program.Settings.SelectedVertex = _afterMouse;
                mainPanel.Refresh();
            }
        }

        private void mouseUp(object sender, MouseEventArgs e)
        {
            Program.Settings.Undo.Push(new MoveVertex(_beforeMouse, _afterMouse, _mouseAffected));
            recalculateBounds();
            mainPanel.Refresh();
            save();
        }

        private void save()
        {
            var vs = Program.Settings.Faces.SelectMany(f => f.Vertices).Distinct().ToList();
            var txtVs = vs.Select(p => $"v {p.X} {p.Y} {p.Z}");
            var txtFs = Program.Settings.Faces.Select(f => $"f {f.Vertices.Select(vs.IndexOf).JoinString(" ")}");
            File.WriteAllLines(Program.Settings.Filename, txtVs.Concat(txtFs));
            Program.Settings.Save(onFailure: SettingsOnFailure.ShowRetryWithCancel);
        }

        void keyDown(object sender, KeyEventArgs e)
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
                            _lastFaceFromVertex != null && Program.Settings.Faces[_lastFaceFromVertexIndex.Value].Vertices.Contains(_lastFaceFromVertex.Value))
                            Program.Settings.SelectedVertex = _lastFaceFromVertex;
                        else if (Program.Settings.SelectedFaceIndex != null)
                            Program.Settings.SelectedVertex = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value].Vertices.FirstOrNull();
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
                                Enumerable.Range(_lastFaceFromVertexIndex.Value + 1, Program.Settings.Faces.Count - _lastFaceFromVertexIndex.Value - 1).Where(i => Program.Settings.Faces[i].Vertices.Contains(_lastFaceFromVertex.Value)).FirstOrNull() ??
                                Enumerable.Range(0, _lastFaceFromVertexIndex.Value + 1).Where(i => Program.Settings.Faces[i].Vertices.Contains(_lastFaceFromVertex.Value)).FirstOrNull();
                            Program.Settings.SelectFace(_lastFaceFromVertexIndex);
                        }
                    }
                    break;

                case "Ctrl+Z": case "Alt+Back": undo(); break;
                case "Ctrl+Y": case "Alt+Shift+Back": redo(); break;

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
                                    Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value] = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value].FlipHidden();
                                    break;

                                case "Delete":
                                    if (Program.Settings.SelectedFaceIndex != null)
                                        execute(new DeleteFace(Program.Settings.SelectedFaceIndex.Value));
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
                            switch (combo)
                            {
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

                                default:
                                    anyChanges = false;
                                    break;
                            }

                            if (moveX != null)
                            {
                                var before = Program.Settings.SelectedVertex.Value;
                                var point = trP(before);
                                var after = revTrP(point.X + moveX.Value, point.Y + moveY, before);
                                execute(new MoveVertex(before, after, getAffected(before)));
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

        private static void execute(UndoItem ui)
        {
            ui.Redo();
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

            var usedVertices = new HashSet<Pt>();
            foreach (var inf in Program.Settings.Faces.Select((f, i) => new { Face = f, Index = i }).OrderBy(x => !x.Face.Hidden))
            {
                var poly = inf.Face.Vertices.Select(v => trP(v).ToPointF()).ToArray();
                e.Graphics.FillPolygon(
                    Program.Settings.IsFaceSelected && inf.Index == Program.Settings.SelectedFaceIndex
                        ? (inf.Face.Hidden ? Brushes.PaleVioletRed : Brushes.MediumVioletRed)
                        : (inf.Face.Hidden ? Brushes.LightSalmon : Brushes.LightGray),
                    poly);
                e.Graphics.DrawPolygon(new Pen(Brushes.DarkGray, 2f) { LineJoin = LineJoin.Round }, poly);
                if (!inf.Face.Hidden)
                    usedVertices.AddRange(inf.Face.Vertices);
            }

            using (var f = new Font("Agency FB", 12f, FontStyle.Regular))
                foreach (var v in usedVertices)
                    e.Graphics.DrawString(v.Y.ToString("0.####"), f, Brushes.Black, trP(v).ToPointF(), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });

            if (!Program.Settings.IsFaceSelected && Program.Settings.SelectedVertex != null)
                e.Graphics.DrawEllipse(new Pen(Color.Red, 2f), new RectangleF(trP(Program.Settings.SelectedVertex.Value).ToPointF() - new SizeF(_selectionSize.Width / 2, _selectionSize.Height / 2), _selectionSize));
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
