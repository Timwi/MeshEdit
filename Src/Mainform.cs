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

        // Alt+Arrow keys
        private Pt? _selectingFromVertex;
        private int? _selectingDirection;
        private bool _selectingDirectionFixed;
        private Pt[] _selectingFrom;
        private int _selectingFromIndex;

        // Drag & drop
        private bool _draggingVertices = false;
        private Pt? _draggingVerticesTo;
        private Tuple<VertexInfo, Pt>[] _draggingAffected;
        private int _draggingIndex;    // the vertex that was dragged, out of multiple selected ones
        private RectangleD? _draggingSelectionRect = null;

        // Mouse move
        private Pt? _highlightVertex;

        // Oemplus key in face mode
        private int? _selectedForFaceMerge = null;

        public Mainform() : base(Program.Settings.MainWindowSettings)
        {
            if (Program.Settings.SelectedFaceIndex != null && Program.Settings.SelectedFaceIndex >= Program.Settings.Faces.Count)
                Program.Settings.SelectedFaceIndex = null;
            Program.Settings.SelectedVertices = Program.Settings.SelectedVertices.Where(v => Program.Settings.Faces.Any(f => f.Locations.Contains(v))).ToList();

            Program.Settings.UpdateUI += updateUi;

            InitializeComponent();
            recalculateBounds();
            updateUi();
        }

        public void updateUi()
        {
            Text = $"Mesh Edit — {(Program.Settings.IsFaceSelected ? "Face" : "Vertex")}";
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
                if ((m = Regex.Match(line, @"^v (-?\d*\.?\d+(?:[eE]-?\d+)?) (-?\d*\.?\d+(?:[eE]-?\d+)?) (-?\d*\.?\d+(?:[eE]-?\d+)?)$")).Success)
                    vertices.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if ((m = Regex.Match(line, @"^vt (-?\d*\.?\d+(?:[eE]-?\d+)?) (-?\d*\.?\d+(?:[eE]-?\d+)?)$")).Success)
                    textures.Add(new PointD(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value)));
                else if ((m = Regex.Match(line, @"^vn (-?\d*\.?\d+(?:[eE]-?\d+)?) (-?\d*\.?\d+(?:[eE]-?\d+)?) (-?\d*\.?\d+(?:[eE]-?\d+)?)$")).Success)
                    normals.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if (line.StartsWith("v"))
                    System.Diagnostics.Debugger.Break();
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

            Program.Settings.Faces = faces.Select(f => new Face(f.Select(ix => new VertexInfo(vertices[ix.Item1], ix.Item2 == -1 ? (PointD?) null : textures[ix.Item2], ix.Item3 == -1 ? (Pt?) null : normals[ix.Item3])).ToArray())).ToList();
            recalculateBounds();
        }

        private void recalculateBounds()
        {
            _minX = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.X));
            _minY = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.Z));
            _boundingW = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.X)) - _minX;
            _boundingH = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.Z)) - _minY;
            _displayRect = fitIntoMaintainAspectRatio(_boundingW, _boundingH, new RectangleD(_paddingX, _paddingY, ClientSize.Width - 2 * _paddingX, ClientSize.Height - 2 * _paddingY));
        }

        private Tuple<Face, int[]>[] getAffected(Pt[] p)
        {
            return Program.Settings.Faces.Select(f => Tuple.Create(f, f.Locations.SelectIndexWhere(p.Contains).ToArray())).Where(inf => inf.Item2.Length > 0).ToArray();
        }

        void mouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left) && Program.Settings.Faces.Count > 0)
            {
                if (_highlightVertex != null)
                {
                    if (Program.Settings.SelectedVertices.Contains(_highlightVertex.Value))
                    {
                        // Shift+Click on selected vertex: unselect it and don’t do dragging
                        if (Ut.Shift)
                        {
                            Program.Settings.SelectedVertices.Remove(_highlightVertex.Value);
                            return;
                        }
                    }
                    else
                    {
                        // Click or Shift+Click on non-selected vertex
                        if (Ut.Shift)
                            Program.Settings.SelectedVertices.Add(_highlightVertex.Value);
                        else
                            Program.Settings.SelectVertex(_highlightVertex);
                    }

                    _draggingVerticesTo = null;
                    _draggingAffected = Program.Settings.Faces.SelectMany(f => f.Vertices.Where(v => Program.Settings.SelectedVertices.Contains(v.Location))).Select(v => Tuple.Create(v, v.Location)).ToArray();
                    _draggingIndex = _draggingAffected.IndexOf(tup => tup.Item2 == _highlightVertex.Value);
                    _highlightVertex = null;
                    _draggingVertices = true;
                    mainPanel.Invalidate();
                }
                else
                {
                    if (!Ut.Shift)
                        Program.Settings.SelectVertex(null);
                    _draggingSelectionRect = new RectangleD(e.X, e.Y, 0, 0);
                }
            }
        }

        void mouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingVertices)
            {
                var beforeMouse = _draggingAffected[_draggingIndex].Item2;
                _draggingVerticesTo = revTrP(e.X, e.Y, beforeMouse);

                for (int i = 0; i < _draggingAffected.Length; i++)
                    _draggingAffected[i].Item1.Location = i == _draggingIndex ? _draggingVerticesTo.Value : _draggingAffected[i].Item2 - beforeMouse + _draggingVerticesTo.Value;
                Program.Settings.SelectedVertices = _draggingAffected.Select(tup => tup.Item1.Location).Distinct().ToList();
                mainPanel.Refresh();
            }
            else if (_draggingSelectionRect != null)
            {
                _draggingSelectionRect = new RectangleD(_draggingSelectionRect.Value.X, _draggingSelectionRect.Value.Y, e.X - _draggingSelectionRect.Value.X, e.Y - _draggingSelectionRect.Value.Y);
                mainPanel.Invalidate();
            }
            else
                updateHighlight(e);
        }

        private void mouseUp(object sender, MouseEventArgs e)
        {
            if (_draggingVertices)
            {
                if (_draggingVerticesTo != null)
                {
                    var beforeMouse = _draggingAffected[_draggingIndex].Item2;
                    Program.Settings.Undo.Push(new MoveVertices(_draggingAffected.Select(tup => Tuple.Create(tup.Item1, tup.Item2, tup.Item2 - beforeMouse + _draggingVerticesTo.Value)).ToArray()));
                    recalculateBounds();
                    updateHighlight(e);
                    mainPanel.Refresh();
                    save();
                    _draggingVerticesTo = null;
                }
                _draggingVertices = false;
                _draggingAffected = null;
            }
            else if (_draggingSelectionRect != null)
            {
                var n = _draggingSelectionRect.Value.Normalize();
                var faces = Ut.Ctrl ? Program.Settings.Faces : Program.Settings.Faces.Where(f => !f.Hidden);
                if (!Ut.Shift)
                    Program.Settings.SelectedVertices = new List<Pt>();
                Program.Settings.SelectedVertices = Program.Settings.SelectedVertices.Union(
                    faces.SelectMany(f => f.Locations).Distinct().Where(v => trP(v).Apply(p => n.Contains(p.X, p.Y)))).ToList();
                _draggingSelectionRect = null;
            }
        }

        private void updateHighlight(MouseEventArgs e)
        {
            _highlightVertex = Program.Settings.Faces.SelectMany(f => f.Locations).Distinct()
                .Select(v => trP(v).Apply(p => new { Vertex = v, Point = p, Distance = Math.Sqrt(Math.Pow(p.X - e.X, 2) + Math.Pow(p.Y - e.Y, 2)) }))
                .Where(inf => inf.Distance <= 10)
                .MinElementOrDefault(inf => inf.Distance, null)?.Vertex;
            mainPanel.Invalidate();
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
            var txtFs = Program.Settings.Faces.Select(f => $"f {f.Vertices.Select(vi => encode(vs.IndexOf(vi.Location), vi.Texture?.Apply(t => ts.IndexOf(t)), vi.Normal?.Apply(n => ns.IndexOf(n)))).JoinString(" ")}");
            File.WriteAllLines(Program.Settings.Filename, txtVs.Concat(txtTs).Concat(txtNs).Concat(txtFs));
            Program.Settings.Save(onFailure: SettingsOnFailure.ShowRetryWithCancel);
        }

        private string encode(int vix, int? tix, int? nix)
        {
            if (tix == null && nix == null)
                return (vix + 1).ToString();
            else if (tix == null)
                return $"{vix + 1}//{nix + 1}";
            else if (nix == null)
                return $"{vix + 1}/{tix + 1}";
            else
                return $"{vix + 1}/{tix + 1}/{nix + 1}";
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
                        //if (Program.Settings.SelectedFaceIndex != null && Program.Settings.SelectedFaceIndex == _lastFaceFromVertexIndex &&
                        //    _lastFaceFromVertex != null && Program.Settings.Faces[_lastFaceFromVertexIndex.Value].Locations.Contains(_lastFaceFromVertex.Value))
                        //    Program.Settings.SelectedVertex = _lastFaceFromVertex;
                        //else if (Program.Settings.SelectedFaceIndex != null)
                        //    Program.Settings.SelectedVertex = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value].Locations.FirstOrNull();
                        //else
                        //    Program.Settings.SelectedVertex = null;
                    }
                    else
                    {
                        Program.Settings.IsFaceSelected = true;
                        Program.Settings.SelectFace(Program.Settings.Faces.SelectIndexWhere(f => Program.Settings.SelectedVertices.All(f.Locations.Contains)).FirstOrNull());
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
                        Program.Settings.SelectedVertices.Clear();
                        Program.Settings.SelectedFaceIndex = null;
                        Program.Settings.IsFaceSelected = false;
                    }
                    break;

                case "Ctrl+S":
                    save();
                    break;

                case "T":
                    using (var dlg = new ManagedForm(Program.Settings.ToolWindowSettings) { Text = "Use custom tool", FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = false, MaximizeBox = false, ControlBox = false, ShowInTaskbar = false })
                    {
                        var cmb = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                        cmb.Items.AddRange(Tool.AllTools);

                        var btnOk = new Button { Text = "&OK" };
                        btnOk.Click += delegate { dlg.DialogResult = DialogResult.OK; };
                        var btnCancel = new Button { Text = "&Cancel" };
                        btnCancel.Click += delegate { dlg.DialogResult = DialogResult.Cancel; };
                        dlg.AcceptButton = btnOk;
                        dlg.CancelButton = btnCancel;

                        var layout = new TableLayoutPanel { Dock = DockStyle.Fill };
                        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 1));
                        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1));
                        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                        layout.Controls.Add(cmb, 0, 0);
                        layout.SetColumnSpan(cmb, 3);
                        layout.Controls.Add(btnOk, 1, 1);
                        layout.Controls.Add(btnCancel, 2, 1);

                        dlg.Controls.Add(layout);
                        if (dlg.ShowDialog() == DialogResult.OK && cmb.SelectedItem != null)
                            ((Tool) cmb.SelectedItem).Execute();
                    }
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
                                    Program.Settings.Execute(new SetHidden(new[] { face }, !face.Hidden));
                                    break;

                                case "Ctrl+H":
                                    Program.Settings.Execute(new SetHidden(Program.Settings.Faces.ToArray(), true));
                                    break;

                                case "Ctrl+Shift+H":
                                    Program.Settings.Execute(new SetHidden(Program.Settings.Faces.ToArray(), false));
                                    break;

                                case "Delete":
                                    if (Program.Settings.SelectedFaceIndex != null)
                                        Program.Settings.Execute(new DeleteFace(Program.Settings.SelectedFaceIndex.Value));
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
                                        Program.Settings.Execute(new AddRemoveFaces(new[] { face1, face2 }, new[] { new Face(newVertices.ToArray(), face1.Hidden && face2.Hidden) }));
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
                        int? moveX = null;
                        int moveY = 0;

                        var processArrow = Ut.Lambda((int direction) =>
                        {
                            if (_selectingFromVertex == null)
                            {
                                _selectingFromVertex = Program.Settings.SelectedVertices[0];
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
                                    Program.Settings.SelectedVertices = new List<Pt> { _selectingFrom[_selectingFromIndex++] };
                            }
                            else if (Math.Abs(direction - _selectingDirection.Value) != 2 && Math.Abs(direction - _selectingDirection.Value) != 6 && _selectingFromIndex >= 2)
                                Program.Settings.SelectedVertices = new List<Pt> { _selectingFrom[--_selectingFromIndex - 1] };

                            anyChanges = false;
                            mainPanel.Invalidate();
                        });

                        switch (combo)
                        {
                            case "Ctrl+C":
                                Clipboard.SetText(Program.Settings.SelectedVertices.Select(v => $"({v.X:R}, {v.Y:R}, {v.Z:R})").JoinString(Environment.NewLine));
                                anyChanges = false;
                                break;

                            case "H":
                            case "Shift+H":
                                Program.Settings.Execute(new SetHidden(Program.Settings.Faces.Where(f => Program.Settings.SelectedVertices.Any(f.Locations.Contains)).ToArray(), !shift));
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

                            case "X": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].X)); anyChanges = false; break;
                            case "Y": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].Y)); anyChanges = false; break;
                            case "Z": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].Z)); anyChanges = false; break;
                            case "Shift+X": replaceVertices(x: ExactConvert.ToDouble(Clipboard.GetText())); break;
                            case "Shift+Y": replaceVertices(y: ExactConvert.ToDouble(Clipboard.GetText())); break;
                            case "Shift+Z": replaceVertices(z: ExactConvert.ToDouble(Clipboard.GetText())); break;

                            case "Alt+Right": case "Alt+Shift+Right": processArrow(0); break;
                            case "Alt+Down": case "Alt+Shift+Down": processArrow(2); break;
                            case "Alt+Left": case "Alt+Shift+Left": processArrow(4); break;
                            case "Alt+Up": case "Alt+Shift+Up": processArrow(6); break;

                            case "D0":
                            case "D1":
                            case "D2":
                            case "D3":
                            case "D4":
                            case "D5":
                            case "D6":
                            case "D7":
                            case "D8":
                            case "D9":
                                {
                                    var rs = Program.Settings.RememberedSelections[combo.Last() - '0'];
                                    if (rs != null)
                                    {
                                        Program.Settings.SelectedVertices = rs.Where(v => Program.Settings.Faces.Any(f => f.Vertices.Contains(v))).Select(v => v.Location).Distinct().ToList();
                                        Program.Settings.IsFaceSelected = false;
                                        anyChanges = false;
                                        mainPanel.Invalidate();
                                    }
                                    break;
                                }

                            case "Shift+D0":
                            case "Shift+D1":
                            case "Shift+D2":
                            case "Shift+D3":
                            case "Shift+D4":
                            case "Shift+D5":
                            case "Shift+D6":
                            case "Shift+D7":
                            case "Shift+D8":
                            case "Shift+D9":
                                {
                                    var rs = Program.Settings.RememberedSelections[combo.Last() - '0'];
                                    if (rs != null)
                                    {
                                        Program.Settings.SelectedVertices = Program.Settings.SelectedVertices.Union(rs.Where(v => Program.Settings.Faces.Any(f => f.Vertices.Contains(v))).Select(v => v.Location)).ToList();
                                        Program.Settings.IsFaceSelected = false;
                                        anyChanges = false;
                                        mainPanel.Invalidate();
                                    }
                                    break;
                                }

                            case "Ctrl+D0":
                            case "Ctrl+D1":
                            case "Ctrl+D2":
                            case "Ctrl+D3":
                            case "Ctrl+D4":
                            case "Ctrl+D5":
                            case "Ctrl+D6":
                            case "Ctrl+D7":
                            case "Ctrl+D8":
                            case "Ctrl+D9":
                                Program.Settings.RememberedSelections[combo.Last() - '0'] = Program.Settings.SelectedVertices.Select(loc => Program.Settings.Faces.SelectMany(f => f.Vertices).FirstOrDefault(v => v.Location == loc)).ToArray();
                                anyChanges = false;
                                break;

                            case "Oemplus":
                                {
                                    if (Program.Settings.SelectedVertices.Count != 2)
                                    {
                                        DlgMessage.ShowInfo("Need exactly two selected vertices to create a new vertex between them.");
                                        break;
                                    }

                                    var sel1 = Program.Settings.SelectedVertices[0];
                                    var sel2 = Program.Settings.SelectedVertices[1];
                                    var affectedFaces = Program.Settings.Faces
                                        .Select(f => Tuple.Create(f, Enumerable.Range(0, f.Vertices.Length)
                                            .Where(i =>
                                                (f.Vertices[i].Location == sel1 && f.Vertices[(i + 1) % f.Vertices.Length].Location == sel2) ||
                                                (f.Vertices[i].Location == sel2 && f.Vertices[(i + 1) % f.Vertices.Length].Location == sel1))
                                            .ToArray()))
                                        .Where(tup => tup.Item2.Length > 0)
                                        .ToArray();
                                    Program.Settings.Execute(new CreateVertex(affectedFaces));
                                    break;
                                }

                            case "Shift+Oemplus":
                                {
                                    if (Program.Settings.SelectedVertices.Count < 3)
                                    {
                                        DlgMessage.ShowInfo("Need at least three vertices to create a face.");
                                        break;
                                    }
                                    Program.Settings.Execute(new AddRemoveFaces(null, new[] { new Face(Program.Settings.SelectedVertices.ToArray()) }));
                                    break;
                                }

                            case "Delete":
                                if (Program.Settings.SelectedVertices.Count > 0)
                                    Program.Settings.Execute(new DeleteVertex(getAffected(Program.Settings.SelectedVertices.ToArray())));
                                break;

                            case "OemMinus":
                                {
                                    if (Program.Settings.SelectedVertices.Count != 2)
                                    {
                                        DlgMessage.ShowInfo("Need exactly two selected vertices.");
                                        break;
                                    }

                                    var sel1 = Program.Settings.SelectedVertices[0];
                                    var sel2 = Program.Settings.SelectedVertices[1];
                                    var affectedFace = Program.Settings.Faces
                                        .Select(f => new { Face = f, Index1 = f.Vertices.IndexOf(v => v.Location == sel1), Index2 = f.Vertices.IndexOf(v => v.Location == sel2) })
                                        .Where(inf => inf.Index1 != -1 && inf.Index2 != -1)
                                        .FirstOrDefault();
                                    if (affectedFace != null)
                                    {
                                        var index1 = Math.Min(affectedFace.Index1, affectedFace.Index2);
                                        var index2 = Math.Max(affectedFace.Index1, affectedFace.Index2);
                                        var newFace1 = new Face(affectedFace.Face.Vertices.Subarray(index1, index2 - index1 + 1), affectedFace.Face.Hidden);
                                        var newFace2 = new Face(affectedFace.Face.Vertices.Subarray(index2).Concat(affectedFace.Face.Vertices.Subarray(0, index1 + 1)).ToArray(), affectedFace.Face.Hidden);
                                        Program.Settings.Execute(new AddRemoveFaces(new[] { affectedFace.Face }, new[] { newFace1, newFace2 }));
                                    }
                                    break;
                                }

                            default:
                                anyChanges = false;
                                break;
                        }

                        if (moveX != null)
                        {
                            Program.Settings.Execute(new MoveVertices(Program.Settings.Faces.SelectMany(f => f.Vertices)
                                .Where(v => Program.Settings.SelectedVertices.Contains(v.Location))
                                .Select(v => Tuple.Create(v, v.Location, trP(v.Location).Apply(point => revTrP(point.X + moveX.Value, point.Y + moveY, v.Location))))
                                .ToArray()));
                        }
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

        private void replaceVertices(double? x = null, double? y = null, double? z = null)
        {
            Program.Settings.Execute(new MoveVertices(Program.Settings.Faces.SelectMany(f => f.Vertices)
                .Where(v => Program.Settings.SelectedVertices.Contains(v.Location))
                .Select(v => Tuple.Create(v, v.Location, v.Location.Set(x, y, z)))
                .ToArray()));
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

            // Selected vertices
            if (!Program.Settings.IsFaceSelected)
                using (var font = new Font("Agency FB", 10f, FontStyle.Regular))
                    for (int i = 0; i < Program.Settings.SelectedVertices.Count; i++)
                    {
                        var vertex = Program.Settings.SelectedVertices[i];
                        var pt = trP(vertex).ToPointF();
                        e.Graphics.DrawEllipse(new Pen(Color.Navy, 2f), new RectangleF(pt - tm(_selectionSize, .5f), _selectionSize));
                        e.Graphics.DrawString((i + 1).ToString(), font, Brushes.Navy, pt + new SizeF(0, -_selectionSize.Height / 2), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far });
                        e.Graphics.DrawString($"{vertex.Y:0.###}", font, Brushes.Black, pt, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });

                        var j = 0;
                        foreach (var t in Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Location == vertex && v.Normal != null).Select(v => v.Normal.Value).Distinct())
                            e.Graphics.DrawString($"({t.X:0.###}, {t.Y:0.###}, {t.Z:0.###})", font, Brushes.CadetBlue, pt + new SizeF(0, 15 * (++j)), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                    }

            // Highlighted vertex
            if (_highlightVertex != null)
                e.Graphics.DrawEllipse(new Pen(Color.Silver, 2f), new RectangleF(trP(_highlightVertex.Value).ToPointF() - new SizeF(_selectionSize.Width, _selectionSize.Height), tm(_selectionSize, 2)));

            // Selected face
            if (Program.Settings.IsFaceSelected && Program.Settings.SelectedFaceIndex != null)
            {
                var face = Program.Settings.Faces[Program.Settings.SelectedFaceIndex.Value];
                e.Graphics.FillPolygon(
                    brush: new SolidBrush(Color.FromArgb(64, Color.Navy)),
                    points: face.Locations.Select(v => trP(v).ToPointF()).ToArray());
            }

            // Select-from “cone” (Alt+Arrow)
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
                    if (Program.Settings.SelectedVertices.Count == 0 || v != Program.Settings.SelectedVertices[0])
                        e.Graphics.DrawEllipse(new Pen(Color.CornflowerBlue, 2f), new RectangleF(trP(v).ToPointF() - new SizeF(_selectionSize.Width / 2, _selectionSize.Height / 2), _selectionSize));

                if (Program.Settings.SelectedVertices.Count > 0)
                    e.Graphics.DrawLine(new Pen(Brushes.Navy, 7.5f) { EndCap = LineCap.ArrowAnchor }, orig, trP(Program.Settings.SelectedVertices[0]).ToPointF());
            }

            // Selection rectangle (drag & drop)
            if (_draggingSelectionRect != null)
            {
                var n = _draggingSelectionRect.Value.Normalize().Round();
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(64, Color.LightBlue)), n);
                e.Graphics.DrawRectangle(new Pen(Color.LightBlue, 2f), n);
            }
        }

        private SizeF tm(SizeF sz, float factor) => new SizeF(sz.Width * factor, sz.Height * factor);

        void paintBuffer(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.High;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.Clear(Color.White);

            var usedVertices = new HashSet<VertexInfo>();
            foreach (var inf in Program.Settings.Faces.Select((f, i) => new { Face = f, Index = i }).OrderBy(x => !x.Face.Hidden))
            {
                var poly = inf.Face.Locations.Select(v => trP(v).ToPointF()).ToArray();
                e.Graphics.FillPolygon(inf.Face.Hidden ? Brushes.LightSalmon : Brushes.LightGray, poly);
                e.Graphics.DrawPolygon(new Pen(Brushes.DarkGray, 2f) { LineJoin = LineJoin.Round }, poly);
                if (!inf.Face.Hidden)
                    usedVertices.AddRange(inf.Face.Vertices);
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
            _highlightVertex = null;
        }

        private void redo()
        {
            if (Program.Settings.Redo.Count == 0)
                return;
            var item = Program.Settings.Redo.Pop();
            Program.Settings.Undo.Push(item);
            item.Redo();
            _highlightVertex = null;
        }
    }
}
