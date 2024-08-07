﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using RT.Json;
using RT.Serialization;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Forms;
using RT.Util.Geometry;

namespace MeshEdit
{
    public partial class Mainform : ManagedForm
    {
        private const int _pxPaddingX = 20;
        private const int _pxPaddingY = 20;
        private static SizeF _selectionSize = new SizeF(10, 10);

        // The region that is supposed to be shown by fitting it into the window size. If the aspect ratio is different, the window shows more.
        private RectangleD _intendedShowingRect;
        // The region that is actually shown on the screen.
        private RectangleD _actualShowingRect;

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

        private bool _aboutToZoom = false;

        public Mainform(bool doOpenFile) : base(Program.Settings.MainWindowSettings)
        {
            Program.Settings.UpdateUI += updateUi;

            InitializeComponent();
            if (doOpenFile)
                openFile();
            updateUi();
        }

        public void updateUi()
        {
            Text = $"Mesh Edit — {(Program.Settings.IsFaceSelected ? "Face" : "Vertex")} — {Program.Settings.Filename ?? "(untitled)"}";
            recalculateBounds();
        }

        private void openFile()
        {
            var vertices = new List<Pt>();
            var textures = new List<PointD>();
            var normals = new List<Pt>();
            var faces = new List<Tuple<int, int, int>[]>();
            string hiddenFacesStr = null;

            foreach (var line in File.ReadAllLines(Program.Settings.Filename))
            {
                Match m;
                if ((m = Regex.Match(line, @"^v (-?\d*\.?\d+(?:[eE][-+]?\d+)?) (-?\d*\.?\d+(?:[eE][-+]?\d+)?) (-?\d*\.?\d+(?:[eE][-+]?\d+)?)$")).Success)
                    vertices.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if ((m = Regex.Match(line, @"^vt (-?\d*\.?\d+(?:[eE][-+]?\d+)?) (-?\d*\.?\d+(?:[eE][-+]?\d+)?)$")).Success)
                    textures.Add(new PointD(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value)));
                else if ((m = Regex.Match(line, @"^vn (-?\d*\.?\d+(?:[eE][-+]?\d+)?) (-?\d*\.?\d+(?:[eE][-+]?\d+)?) (-?\d*\.?\d+(?:[eE][-+]?\d+)?)$")).Success)
                    normals.Add(new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
                else if (line.StartsWith("v"))
                    System.Diagnostics.Debugger.Break();
                else if (line.StartsWith("f "))
                    faces.Add(line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s =>
                        {
                            var ixs = s.Split(new[] { '/' });
                            var vix = int.Parse(ixs[0]) - 1;
                            var tix = ixs.Length > 1 && ixs[1].Length > 0 ? int.Parse(ixs[1]) - 1 : -1;
                            var nix = ixs.Length > 2 && ixs[2].Length > 0 ? int.Parse(ixs[2]) - 1 : -1;
                            return Tuple.Create(vix, tix, nix);
                        }).ToArray());
                else if (line.StartsWith("#h "))
                    hiddenFacesStr = line.Substring(3);
                else if (line.StartsWith("o "))
                    Program.Settings.ObjectName = line.Substring(2);
            }

            Program.Settings.SelectVertex(null);
            Program.Settings.Faces = faces.Select(f => new Face(f.Select(ixs => new VertexInfo(vertices[ixs.Item1], ixs.Item2 == -1 ? (PointD?) null : textures[ixs.Item2], ixs.Item3 == -1 ? (Pt?) null : normals[ixs.Item3])).ToArray())).ToList();
            if (hiddenFacesStr != null)
                foreach (var segment in hiddenFacesStr.Split(','))
                    if (int.TryParse(segment, out var ix) && ix >= 0 && ix < Program.Settings.Faces.Count)
                        Program.Settings.Faces[ix].Hidden = true;
            recalculateBounds();
        }

        private void recalculateBounds()
        {
            if (Program.Settings.ShowingRect != null)
                _intendedShowingRect = Program.Settings.ShowingRect.Value;
            else if (Program.Settings.Faces.Count > 0)
            {
                var minX = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.X));
                var minY = Program.Settings.Faces.Min(f => f.Locations.Min(p => p.Z));
                var boundingW = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.X)) - minX;
                var boundingH = Program.Settings.Faces.Max(f => f.Locations.Max(p => p.Z)) - minY;
                var paddingX = boundingW / ClientSize.Width * _pxPaddingX;
                var paddingY = boundingH / ClientSize.Height * _pxPaddingY;
                _intendedShowingRect = new RectangleD(minX - paddingX, minY - paddingY, boundingW + 2 * paddingX, boundingH + 2 * paddingY);
            }
            else
                _intendedShowingRect = new RectangleD(0, 0, 1, 1);

            if ((double) ClientSize.Width / ClientSize.Height > _intendedShowingRect.Width / _intendedShowingRect.Height)
            {
                var newWidth = _intendedShowingRect.Height / ClientSize.Height * ClientSize.Width;
                _actualShowingRect = new RectangleD(
                    _intendedShowingRect.Left - (newWidth - _intendedShowingRect.Width) / 2,
                    _intendedShowingRect.Top,
                    newWidth,
                    _intendedShowingRect.Height
                );
            }
            else
            {
                var newHeight = _intendedShowingRect.Width / ClientSize.Width * ClientSize.Height;
                _actualShowingRect = new RectangleD(
                    _intendedShowingRect.Left,
                    _intendedShowingRect.Top - (newHeight - _intendedShowingRect.Height) / 2,
                    _intendedShowingRect.Width,
                    newHeight
                );
            }

            mainPanel.Refresh();
        }

        private List<Tuple<Face, int[]>> getAffected(List<Pt> p)
        {
            return Program.Settings.Faces.Select(f => Tuple.Create(f, f.Locations.SelectIndexWhere(p.Contains).ToArray())).Where(inf => inf.Item2.Length > 0).ToList();
        }

        void mouseDown(object sender, MouseEventArgs e)
        {
            var mousePos = new PointD(e.X, e.Y);
            if (e.Button.HasFlag(MouseButtons.Left) && Program.Settings.Faces.Count > 0)
            {
                var potentialFaces = Program.Settings.Faces.Where(f => new PolygonD(f.Vertices.Select(v => trP(v.Location))).ContainsPoint(mousePos)).ToArray();

                if (_highlightVertex == null && potentialFaces.Length > 0 && !_aboutToZoom && !UtWin.Ctrl)
                {
                    var potentialSelectedFace = potentialFaces.FirstOrDefault(Program.Settings.SelectedFaces.Contains);
                    if (UtWin.Shift && potentialSelectedFace != null)
                    {
                        // Deselect a face
                        Program.Settings.SelectedFaces.Remove(potentialSelectedFace);
                        updateUi();
                    }
                    else if (UtWin.Shift)
                    {
                        // Add a face to the selection
                        Program.Settings.SelectedFaces.Add(potentialFaces.First(pf => !Program.Settings.SelectedFaces.Contains(pf)));
                        updateUi();
                    }
                    else if (Program.Settings.IsFaceSelected && Program.Settings.SelectedFaces.Count == 1)
                        Program.Settings.SelectFace(potentialFaces[(potentialFaces.IndexOf(Program.Settings.SelectedFaces[0]) + 1) % potentialFaces.Length]);
                    else
                        Program.Settings.SelectFace(potentialFaces[0]);
                }
                else if (_highlightVertex == null || _aboutToZoom || UtWin.Ctrl)
                {
                    if (!UtWin.Shift && !_aboutToZoom)
                        Program.Settings.SelectVertex(null);
                    _draggingSelectionRect = new RectangleD(mousePos.X, mousePos.Y, 0, 0);
                }
                else
                {
                    if (!Program.Settings.IsFaceSelected && Program.Settings.SelectedVertices.Contains(_highlightVertex.Value))
                    {
                        // Shift+Click on selected vertex: unselect it and don’t do dragging
                        if (UtWin.Shift)
                        {
                            Program.Settings.SelectedVertices.Remove(_highlightVertex.Value);
                            return;
                        }
                    }
                    else
                    {
                        // Click or Shift+Click on non-selected vertex
                        if (UtWin.Shift && !Program.Settings.IsFaceSelected)
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
            }
        }

        void mouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingVertices)
            {
                var beforeMouse = _draggingAffected[_draggingIndex].Item2;
                _draggingVerticesTo = revTrP(e.X, e.Y, beforeMouse);

                for (int i = 0; i < _draggingAffected.Length; i++)
                {
                    var p = Program.Settings.SelectedVertices.IndexOf(v => v == _draggingAffected[i].Item1.Location);
                    _draggingAffected[i].Item1.Location = i == _draggingIndex ? _draggingVerticesTo.Value : _draggingAffected[i].Item2 - beforeMouse + _draggingVerticesTo.Value;
                    if (p != -1)
                        Program.Settings.SelectedVertices[p] = _draggingAffected[i].Item1.Location;
                }
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
                    updateHighlight(e);
                    save();
                    _draggingVerticesTo = null;
                    recalculateBounds();
                }
                _draggingVertices = false;
                _draggingAffected = null;
            }
            else if (_draggingSelectionRect != null)
            {
                if (_aboutToZoom)
                {
                    _aboutToZoom = false;
                    Cursor = Cursors.Arrow;
                    var rect = _draggingSelectionRect.Value.Normalize();
                    var topLeft = revTrP(rect.Left, rect.Top);
                    var bottomRight = revTrP(rect.Right, rect.Bottom);
                    Program.Settings.ShowingRect = _intendedShowingRect = new RectangleD(topLeft.X, topLeft.Z, bottomRight.X - topLeft.X, bottomRight.Z - topLeft.Z);
                    recalculateBounds();
                }
                else
                {
                    var n = _draggingSelectionRect.Value.Normalize();
                    var faces = UtWin.Ctrl ? Program.Settings.Faces : Program.Settings.Faces.Where(f => !f.Hidden);
                    if (!UtWin.Shift)
                        Program.Settings.SelectedVertices = new List<Pt>();
                    Program.Settings.SelectedVertices = Program.Settings.SelectedVertices.Union(
                        faces.SelectMany(f => f.Locations).Distinct().Where(v => trP(v).Apply(p => n.Contains(p.X, p.Y)))).ToList();
                }
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
            if (Program.Settings.Filename != null)
            {
                var vs = Program.Settings.Faces.SelectMany(f => f.Locations).Distinct().ToList();
                var ts = Program.Settings.Faces.SelectMany(f => f.Textures).Distinct().ToList();
                var ns = Program.Settings.Faces.SelectMany(f => f.Normals).Distinct().ToList();
                var txtVs = vs.Select(p => $"v {p.X} {p.Y} {p.Z}");
                var txtTs = ts.Select(t => $"vt {t.X} {t.Y}");
                var txtNs = ns.Select(n => $"vn {n.X} {n.Y} {n.Z}");
                var txtFs = Program.Settings.Faces.Select(f => $"f {f.Vertices.Select(vi => encode(vs.IndexOf(vi.Location), vi.Texture?.Apply(t => ts.IndexOf(t)), vi.Normal?.Apply(n => ns.IndexOf(n)))).JoinString(" ")}");
                var txtHs = "#h " + Program.Settings.Faces.SelectIndexWhere(f => f.Hidden).JoinString(",");
                File.WriteAllLines(Program.Settings.Filename,
                    new[] { Program.Settings.ObjectName?.Apply(name => $"o {name}") }
                        .Concat(txtVs)
                        .Concat(txtTs)
                        .Concat(txtNs)
                        .Concat(new[] { Program.Settings.ObjectName?.Apply(name => $"g {name}") })
                        .Concat(txtFs)
                        .Concat(txtHs)
                        .Where(s => s != null));
            }

            Program.Settings.Save(onFailure: SettingsOnFailure.ShowRetryWithCancel);
        }

        private string encode(int vix, int? tix, int? nix) =>
            tix == null && nix == null ? (vix + 1).ToString() :
            tix == null ? $"{vix + 1}//{nix + 1}" :
            nix == null ? $"{vix + 1}/{tix + 1}" :
            $"{vix + 1}/{tix + 1}/{nix + 1}";

        private ToolInfo[] _tools;

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
                        Program.Settings.SelectVertices(Program.Settings.SelectedFaces.SelectMany(f => f.Vertices).Select(v => v.Location).Distinct());
                    else
                        Program.Settings.SelectFaces(Program.Settings.Faces.Where(f => f.Vertices.All(v => Program.Settings.SelectedVertices.Contains(v.Location))));
                    break;

                case "Ctrl+Z": case "Alt+Back": undo(); break;
                case "Ctrl+Y": case "Alt+Shift+Back": redo(); break;

                case "Ctrl+A":
                    Program.Settings.SelectVertices(Program.Settings.Faces.SelectMany(f => f.Vertices).Select(v => v.Location).Distinct());
                    break;

                case "Ctrl+Shift+N":
                    Program.Settings.ShowNormals = !Program.Settings.ShowNormals;
                    mainPanel.Invalidate();
                    break;
                case "Ctrl+Shift+T":
                    Program.Settings.ShowTextures = !Program.Settings.ShowTextures;
                    mainPanel.Invalidate();
                    break;

                case "Ctrl+N":
                    Program.Settings.Faces.Clear();
                    Program.Settings.Filename = null;
                    Program.Settings.Undo.Clear();
                    Program.Settings.Redo.Clear();
                    Program.Settings.Deselect();
                    updateUi();
                    break;

                case "Ctrl+O":
                    using (var dlg = new OpenFileDialog { DefaultExt = "obj", Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*" })
                    {
                        if (Program.Settings.LastDir != null)
                            dlg.InitialDirectory = Program.Settings.LastDir;
                        var result = dlg.ShowDialog();
                        if (result == DialogResult.Cancel)
                            break;
                        Program.Settings.Filename = dlg.FileName;
                        Program.Settings.LastDir = Path.GetDirectoryName(dlg.FileName);
                        openFile();
                        Program.Settings.Undo.Clear();
                        Program.Settings.Redo.Clear();
                        Program.Settings.Deselect();
                    }
                    break;

                case "Ctrl+S":
                    if (Program.Settings.Filename == null)
                    {
                        using (var dlg = new SaveFileDialog { DefaultExt = "obj", Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*" })
                        {
                            if (Program.Settings.LastDir != null)
                                dlg.InitialDirectory = Program.Settings.LastDir;
                            var result = dlg.ShowDialog();
                            if (result == DialogResult.Cancel)
                                break;
                            Program.Settings.Filename = dlg.FileName;
                            Program.Settings.LastDir = dlg.InitialDirectory;
                            updateUi();
                        }
                    }
                    save();
                    break;

                case "OemOpenBrackets": // [
                    if (_aboutToZoom)
                    {
                        _aboutToZoom = false;
                        Cursor = Cursors.Arrow;
                    }
                    else if (Program.Settings.Faces.Count > 0)
                    {
                        _aboutToZoom = true;
                        Cursor = Cursors.Cross;
                    }
                    anyChanges = false;
                    break;

                case "Oem6":    // ]
                    Program.Settings.ShowingRect = null;
                    recalculateBounds();
                    anyChanges = false;
                    break;

                case "T":
                    anyChanges = false;
                    using (var dlg = new ManagedForm(Program.Settings.ToolWindowSettings) { Text = "Use custom tool", FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = false, MaximizeBox = false, ControlBox = false, ShowInTaskbar = false })
                    {
                        if (_tools == null)
                            _tools = typeof(Tools).GetMethods()
                                .Select(m => ToolInfo.CreateFromMethod(m))
                                .Where(inf => inf != null)
                                .OrderBy(t => t.Attribute.ReadableName).ToArray();

                        var cmb = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                        cmb.Items.AddRange(_tools);

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
                        {
                            var ti = (ToolInfo) cmb.SelectedItem;
                            var args = Program.Settings.LastToolArguments.Get(ti.Method.Name, null)?.ToArray() ?? new object[ti.Parameters.Length];
                            if (args.Length != ti.Parameters.Length)
                                Array.Resize(ref args, ti.Parameters.Length);
                            var paramNames = ti.Method.GetParameters().Select(p => p.Name).ToArray();
                            for (int i = 0; i < ti.Parameters.Length; i++)
                                if (!ti.Parameters[i].AskForValue(paramNames[i], ref args[i]))
                                    goto outtaHere;
                            Program.Settings.LastToolArguments[ti.Method.Name] = args;
                            foreach (var extraneousKey in Program.Settings.LastToolArguments.Keys.Except(_tools.Select(t => t.Method.Name)).ToArray())
                                Program.Settings.LastToolArguments.Remove(extraneousKey);
                            ti.Method.Invoke(null, args);
                            anyChanges = true;
                        }
                    }
                    outtaHere:
                    break;

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
                    if (rs is Face[] faces)
                        Program.Settings.SelectFaces(faces);
                    else if (rs is Pt[] vertices)
                        Program.Settings.SelectVertices(vertices);
                    anyChanges = false;
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
                    if (rs is Face[] faces)
                        Program.Settings.SelectFaces(faces.Union(Program.Settings.IsFaceSelected ? Program.Settings.SelectedFaces : Enumerable.Empty<Face>()));
                    else if (rs is Pt[] vertices)
                        Program.Settings.SelectVertices(vertices.Union(Program.Settings.IsFaceSelected ? Enumerable.Empty<Pt>() : Program.Settings.SelectedVertices));
                    anyChanges = false;
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
                    Program.Settings.RememberedSelections[combo.Last() - '0'] = Program.Settings.IsFaceSelected
                        ? (object) Program.Settings.SelectedFaces.ToArray()
                        : Program.Settings.SelectedVertices.ToArray();
                    break;

                case "Tab":
                case "Shift+Tab":
                    Program.Settings.SelectFace((Program.Settings.SelectedFaces.Select(f => Program.Settings.Faces.IndexOf(f)).FirstOrDefault(shift ? Program.Settings.Faces.Count : -1) + (shift ? Program.Settings.Faces.Count - 1 : 1)) % Program.Settings.Faces.Count);
                    break;

                default:
                    if (Program.Settings.IsFaceSelected && Program.Settings.SelectedFaces.Count > 0)
                    {
                        switch (combo)
                        {
                            case "Ctrl+C":
                                Clipboard.SetText(ClassifyJson.Serialize(Program.Settings.SelectedFaces).ToStringIndented());
                                anyChanges = false;
                                break;

                            case "Ctrl+V":
                                try { Program.Settings.Execute(new AddRemoveFaces(null, ClassifyJson.Deserialize<Face[]>(JsonValue.Parse(Clipboard.GetText())))); }
                                catch { DlgMessage.Show("The text in the clipboard could not be parsed as a set of faces. (If you copied a set of vertices instead of faces, you cannot paste that.)", "Paste faces", DlgType.Error); }
                                break;

                            case "H":
                                Program.Settings.Execute(new SetHidden(Program.Settings.SelectedFaces.ToArray(), !Program.Settings.SelectedFaces.First().Hidden));
                                break;

                            case "R":
                                Program.Settings.Execute(new ReverseFaces(Program.Settings.SelectedFaces.ToArray()));
                                break;

                            case "Delete":
                                Program.Settings.Execute(new AddRemoveFaces(Program.Settings.SelectedFaces.ToArray(), new Face[0]));
                                break;

                            case "Oemplus":
                                if (Program.Settings.SelectedFaces.Count < 2)
                                {
                                    DlgMessage.Show("Need at least two faces for merge.", "Merge faces", DlgType.Info);
                                    break;
                                }

                                var removedFaces = new List<Face>();
                                var addedFaces = new List<Face>();
                                while (true)
                                {
                                    var pairNullable = Program.Settings.SelectedFaces.UniquePairs().FirstOrNull(tup => tup.Item1.Locations.Intersect(tup.Item2.Locations).Skip(1).Any());
                                    if (pairNullable == null)
                                        break;
                                    var pair = pairNullable.Value;

                                    var face1 = pair.Item1;
                                    var face2 = pair.Item2;
                                    var commonLocs = face1.Locations.Intersect(face2.Locations).ToArray();
                                    if (commonLocs.Length < 2)
                                    {
                                        DlgMessage.Show("The selected faces do not have more than one vertex in common.", "Merge faces", DlgType.Info);
                                        break;
                                    }
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

                                    var ix = addedFaces.IndexOf(face1);
                                    if (ix == -1)
                                        removedFaces.Add(face1);
                                    else
                                        addedFaces.RemoveAt(ix);
                                    ix = addedFaces.IndexOf(face2);
                                    if (ix == -1)
                                        removedFaces.Add(face2);
                                    else
                                        addedFaces.RemoveAt(ix);
                                    var newFace = new Face(newVertices.ToArray(), face1.Hidden && face2.Hidden);
                                    addedFaces.Add(newFace);
                                    Program.Settings.SelectedFaces.Remove(face1);
                                    Program.Settings.SelectedFaces.Remove(face2);
                                    Program.Settings.SelectedFaces.Add(newFace);
                                }
                                Program.Settings.Execute(new AddRemoveFaces(removedFaces.ToArray(), addedFaces.ToArray()));
                                break;

                            default:
                                anyChanges = false;
                                break;
                        }
                    }
                    else if (!Program.Settings.IsFaceSelected && Program.Settings.SelectedVertices.Count > 0)
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

                        double result1, result2, result3;
                        string[] strSplit;
                        string clip;
                        bool x = false, y = false;
                        switch (combo)
                        {
                            case "Ctrl+C":
                                Clipboard.SetText(Program.Settings.SelectedVertices.Select(v => $"({v.X:R}, {v.Y:R}, {v.Z:R})").JoinString(Environment.NewLine));
                                anyChanges = false;
                                break;

                            case "Ctrl+V":
                                var matches = Clipboard.GetText().Replace("\r", "").Split('\n').Select(r => Regex.Match(r, @"^\s*\(\s*(-?\d*\.?\d+(?:e\d+)?)\s*,\s*(-?\d*\.?\d+(?:e\d+)?)\s*,\s*(-?\d*\.?\d+(?:e\d+)?)\s*\)\s*$")).ToArray();
                                if (matches.Any(m => !m.Success) || matches.Length != Program.Settings.SelectedVertices.Count)
                                {
                                    DlgMessage.Show("The clipboard does not contain a set of coordinates equal to the number of currently selected vertices.", "Error", DlgType.Error);
                                    break;
                                }
                                var vertices = matches.Select(m =>
                                {
                                    try { return new Pt(double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)); }
                                    catch { return (Pt?) null; }
                                }).ToArray();
                                if (vertices.Any(v => v == null))
                                {
                                    DlgMessage.Show($"Vertex #{vertices.IndexOf(v => v == null) + 1} in the clipboard does not parse.", "Error", DlgType.Error);
                                    break;
                                }
                                Program.Settings.Execute(new MoveVertices(vertices.Select((v, ix) => Tuple.Create(Program.Settings.SelectedVertices[ix], v.Value)).ToArray()));
                                break;

                            case "H":
                            case "Shift+H":
                                Program.Settings.Execute(new SetHidden(Program.Settings.Faces.Where(f => Program.Settings.SelectedVertices.Any(f.Locations.Contains)).ToArray(), !shift));
                                break;

                            case "Shift+F":
                                Program.Settings.SelectFaces(Program.Settings.Faces.Where(f => !f.Hidden && f.Vertices.Any(v => Program.Settings.SelectedVertices.Contains(v.Location))));
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

                            case "Shift+Left":
                            case "Shift+Right":
                            case "Shift+Up":
                            case "Shift+Down":
                            case "Shift+PageUp":
                            case "Shift+PageDown":
                                tryAgain:
                                var numberStr = InputBox.GetLine("By how much?", "1", "Move vertices");
                                if (numberStr == null)
                                    break;
                                double number;
                                if (!double.TryParse(numberStr, out number))
                                {
                                    DlgMessage.Show("That is not a valid number.", DlgType.Info);
                                    goto tryAgain;
                                }
                                double moveXA = 0, moveYA = 0, moveZA = 0;
                                switch (combo)
                                {
                                    case "Shift+Left": moveXA = -number; break;
                                    case "Shift+Right": moveXA = number; break;
                                    case "Shift+Up": moveZA = -number; break;
                                    case "Shift+Down": moveZA = number; break;
                                    case "Shift+PageUp": moveYA = -number; break;
                                    case "Shift+PageDown": moveYA = number; break;
                                }
                                Program.Settings.Execute(new MoveVertices(Program.Settings.Faces.SelectMany(f => f.Vertices)
                                    .Where(v => Program.Settings.SelectedVertices.Contains(v.Location))
                                    .Select(v => Tuple.Create(v, v.Location, new Pt(v.Location.X + moveXA, v.Location.Y + moveYA, v.Location.Z + moveZA)))
                                    .ToArray()));
                                break;

                            case "X": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].X)); anyChanges = false; break;
                            case "Y": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].Y)); anyChanges = false; break;
                            case "Z": Clipboard.SetText(ExactConvert.ToString(Program.Settings.SelectedVertices[0].Z)); anyChanges = false; break;
                            case "Shift+X": if (ExactConvert.Try(Clipboard.GetText(), out result1)) { replaceVertices(x: result1); } break;
                            case "Shift+Y": if (ExactConvert.Try(Clipboard.GetText(), out result1)) { replaceVertices(y: result1); } break;
                            case "Shift+Z": if (ExactConvert.Try(Clipboard.GetText(), out result1)) { replaceVertices(z: result1); } break;
                            case "Ctrl+Shift+X": x = true; goto case "Ctrl+Shift+Z";
                            case "Ctrl+Shift+Y": y = true; goto case "Ctrl+Shift+Z";
                            case "Ctrl+Shift+Z":
                                var input = InputBox.GetLine("Coordinate?", Program.Settings.SelectedVertices.DefaultIfEmpty().Average(p => x ? p.X : y ? p.Y : p.Z).ToString(), $"Set {(x ? "X" : y ? "Y" : "Z")} coordinate directly");
                                if (input != null && ExactConvert.Try(input, out result1))
                                    replaceVertices(x ? result1 : (double?) null, y ? result1 : (double?) null, !x && !y ? result1 : (double?) null);
                                break;

                            case "P":
                                Clipboard.SetText(Program.Settings.SelectedVertices.Select(t => $"{t.X:R},{t.Y:R},{t.Z:R}").JoinString(Environment.NewLine));
                                anyChanges = false;
                                break;
                            case "Shift+P":
                                if (!(clip = Clipboard.GetText()).Contains('\n') && (strSplit = clip.Split(',')).Length == 3 && ExactConvert.Try(strSplit[0], out result1) && ExactConvert.Try(strSplit[1], out result2) && ExactConvert.Try(strSplit[2], out result3))
                                    replaceVertices(result1, result2, result3);
                                else if ((strSplit = Clipboard.GetText().Trim().Split('\n')).Length == Program.Settings.SelectedVertices.Count && strSplit.All(s => s.Split(',').Length == 3))
                                {
                                    var conv = strSplit
                                        .Select(s => s.Split(','))
                                        .Select(arr => new { X = ExactConvert.Try(arr[0], out double result) ? result : (double?) null, Y = ExactConvert.Try(arr[1], out result) ? result : (double?) null, Z = ExactConvert.Try(arr[2], out result) ? result : (double?) null })
                                        .ToArray();
                                    if (conv.Any(c => c.X == null || c.Y == null || c.Z == null))
                                        break;
                                    Program.Settings.Execute(new MoveVertices(Enumerable.Range(0, strSplit.Length)
                                        .Select(ix => Tuple.Create(Program.Settings.SelectedVertices[ix], new Pt(conv[ix].X.Value, conv[ix].Y.Value, conv[ix].Z.Value)))
                                        .ToArray()));
                                }
                                break;

                            case "N":
                            {
                                var vertexInfos = Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Normal != null && Program.Settings.SelectedVertices.Contains(v.Location)).ToArray();
                                Clipboard.SetText(vertexInfos.Select(v => v.Normal.Value).Select(n => $"{n.X:R},{n.Y:R},{n.Z:R}").JoinString(Environment.NewLine));
                                anyChanges = false;
                            }
                            break;
                            case "Shift+N":
                                if (!(clip = Clipboard.GetText()).Contains('\n') && (strSplit = clip.Split(',')).Length == 3 && ExactConvert.Try(strSplit[0], out result1) && ExactConvert.Try(strSplit[1], out result2) && ExactConvert.Try(strSplit[2], out result3))
                                    replaceNormals(result1, result2, result3);
                                else
                                {
                                    var vertexInfos = Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Normal != null && Program.Settings.SelectedVertices.Contains(v.Location)).ToArray();
                                    if ((strSplit = Clipboard.GetText().Trim().Split('\n')).Length == vertexInfos.Length && strSplit.All(s => s.Split(',').Length == 3))
                                    {
                                        var conv = strSplit
                                            .Select(s => s.Split(','))
                                            .Select(arr => new
                                            {
                                                X = ExactConvert.Try(arr[0], out double result) ? result : (double?) null,
                                                Y = ExactConvert.Try(arr[1], out result) ? result : (double?) null,
                                                Z = ExactConvert.Try(arr[2], out result) ? result : (double?) null
                                            })
                                            .ToArray();
                                        if (conv.Any(c => c.X == null || c.Y == null || c.Z == null) || conv.Length != vertexInfos.Length)
                                            break;
                                        Program.Settings.Execute(new MoveVertices(vertexInfos.Select((v, ix) => Tuple.Create(v, v.Location, v.Location, v.Normal, new Pt(conv[ix].X.Value, conv[ix].Y.Value, conv[ix].Z.Value).Nullable())).ToArray()));
                                    }
                                }
                                break;

                            case "U":
                            {
                                var vertexInfos = Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Texture != null && Program.Settings.SelectedVertices.Contains(v.Location)).ToArray();
                                Clipboard.SetText(vertexInfos.Select(v => v.Texture.Value).Select(n => $"{n.X:R},{n.Y:R}").JoinString(Environment.NewLine));
                                anyChanges = false;
                            }
                            break;
                            case "Shift+U":
                                if (!(clip = Clipboard.GetText()).Contains('\n') && (strSplit = clip.Split(',')).Length == 2 && ExactConvert.Try(strSplit[0], out result1) && ExactConvert.Try(strSplit[1], out result2))
                                    replaceTextures(result1, result2);
                                else
                                {
                                    var vertexInfos = Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Texture != null && Program.Settings.SelectedVertices.Contains(v.Location)).ToArray();
                                    if ((strSplit = Clipboard.GetText().Trim().Split('\n')).Length == vertexInfos.Length && strSplit.All(s => s.Split(',').Length == 2))
                                    {
                                        var conv = strSplit
                                            .Select(s => s.Split(','))
                                            .Select(arr => new
                                            {
                                                X = ExactConvert.Try(arr[0], out double result) ? result : (double?) null,
                                                Y = ExactConvert.Try(arr[1], out result) ? result : (double?) null
                                            })
                                            .ToArray();
                                        if (conv.Any(c => c.X == null || c.Y == null) || conv.Length != vertexInfos.Length)
                                            break;
                                        Program.Settings.Execute(new ModifyTextureCoordinates(vertexInfos.Select((v, ix) => Tuple.Create(v, v.Texture, new PointD(conv[ix].X.Value, conv[ix].Y.Value).Nullable())).ToArray()));
                                    }
                                }
                                break;

                            case "Alt+Right": case "Alt+Shift+Right": processArrow(0); break;
                            case "Alt+Down": case "Alt+Shift+Down": processArrow(2); break;
                            case "Alt+Left": case "Alt+Shift+Left": processArrow(4); break;
                            case "Alt+Up": case "Alt+Shift+Up": processArrow(6); break;

                            case "R":
                                Program.Settings.SelectedVertices.Reverse();
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
                                Program.Settings.Execute(new AddRemoveFaces(null, new[] { new Face(Program.Settings.SelectedVertices.Select(v => new VertexInfo(v, new PointD(0, 0), new Pt(0, 1, 0))).ToArray()) }));
                                break;
                            }

                            case "Delete":
                                if (Program.Settings.SelectedVertices.Count > 0)
                                {
                                    var affected = getAffected(Program.Settings.SelectedVertices);
                                    var removeAffected = new List<Tuple<Face, int[]>>();
                                    var deleted = new List<Face>();
                                    foreach (var aff in affected.Where(af => af.Item2.Length >= af.Item1.Vertices.Length - 2))
                                    {
                                        removeAffected.Add(aff);
                                        deleted.Add(aff.Item1);
                                    }
                                    affected.RemoveRange(removeAffected);
                                    Program.Settings.Execute(new DeleteVertices(affected.ToArray(), deleted.ToArray()));
                                }
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
                                var affectedFaces = Program.Settings.Faces
                                    .Select(f => new { Face = f, Index1 = f.Vertices.IndexOf(v => v.Location == sel1), Index2 = f.Vertices.IndexOf(v => v.Location == sel2) })
                                    .Where(inf => inf.Index1 != -1 && inf.Index2 != -1)
                                    .Take(2)
                                    .ToArray();
                                if (affectedFaces.Length < 1)
                                    DlgMessage.ShowInfo("The selected vertices do not have a face in common.");
                                else if (affectedFaces.Length > 1)
                                    DlgMessage.ShowInfo("The selected vertices have more than one face in common.");
                                else
                                {
                                    var affectedFace = affectedFaces[0];
                                    var index1 = Math.Min(affectedFace.Index1, affectedFace.Index2);
                                    var index2 = Math.Max(affectedFace.Index1, affectedFace.Index2);
                                    if (index2 == index1 + 1 || (index1 == 0 && index2 == affectedFace.Face.Vertices.Length - 1))
                                        DlgMessage.ShowInfo("The selected vertices are adjacent.");
                                    else
                                    {
                                        var newFace1 = new Face(affectedFace.Face.Vertices.Subarray(index1, index2 - index1 + 1), affectedFace.Face.Hidden);
                                        var newFace2 = new Face(affectedFace.Face.Vertices.Subarray(index2).Concat(affectedFace.Face.Vertices.Subarray(0, index1 + 1)).ToArray(), affectedFace.Face.Hidden);
                                        Program.Settings.Execute(new AddRemoveFaces(new[] { affectedFace.Face }, new[] { newFace1, newFace2 }));
                                    }
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
                                .Select(v => roundedMove(v, moveX.Value, moveY))
                                .ToArray()));
                        }
                    }
                    else
                        anyChanges = false;
                    break;
            }

            if (anyChanges)
            {
                mainPanel.Refresh();
                save();
            }
        }

        private double roundedMove(double before, double after)
        {
            if (before == after)
                return after;
            var factor = 1.0;
            var iter = 0;
            while (Math.Truncate(before * factor) == Math.Truncate(after * factor))
            {
                factor *= 10;
                iter++;
                if (iter >= 64)
                    return after;
            }
            factor *= 10;
            return Math.Round(after * factor) / factor;
        }

        private Tuple<VertexInfo, Pt, Pt> roundedMove(VertexInfo v, int moveX, int moveY)
        {
            var newLocation = trP(v.Location).Apply(point => revTrP(point.X + moveX, point.Y + moveY, v.Location));
            return Tuple.Create(v, v.Location, new Pt(roundedMove(v.Location.X, newLocation.X), roundedMove(v.Location.Y, newLocation.Y), roundedMove(v.Location.Z, newLocation.Z)));
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

        private void replaceNormals(double x, double y, double z)
        {
            Program.Settings.Execute(new MoveVertices(Program.Settings.Faces.SelectMany(f => f.Vertices)
                .Where(v => Program.Settings.SelectedVertices.Contains(v.Location))
                .Select(v => Tuple.Create(v, v.Location, v.Location, v.Normal, new Pt(x, y, z).Nullable()))
                .ToArray()));
        }

        private void replaceTextures(double x, double y)
        {
            Program.Settings.Execute(new ModifyTextureCoordinates(Program.Settings.Faces.SelectMany(f => f.Vertices)
                .Where(v => Program.Settings.SelectedVertices.Contains(v.Location))
                .Select(v => Tuple.Create(v, v.Texture, new PointD(x, y).Nullable()))
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
                using (var fontBold = new Font("Agency FB", 10f, FontStyle.Bold))
                    for (int i = 0; i < Program.Settings.SelectedVertices.Count; i++)
                    {
                        var vertex = Program.Settings.SelectedVertices[i];
                        var pt = trP(vertex).ToPointF();
                        e.Graphics.DrawEllipse(new Pen(Color.Navy, 2f), new RectangleF(pt - tm(_selectionSize, .5f), _selectionSize));
                        e.Graphics.DrawString((i + 1).ToString(), font, Brushes.Navy, pt + new SizeF(0, -_selectionSize.Height / 2), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far });
                        var textY = _selectionSize.Height / 2;
                        e.Graphics.DrawString($"{vertex}", font, Brushes.Black, pt + new SizeF(0, textY), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });

                        if (Program.Settings.ShowNormals)
                        {
                            foreach (var nrml in Program.Settings.Faces.SelectMany(f => f.Vertices).Where(v => v.Location == vertex && v.Normal != null).Select(v => v.Normal.Value).Distinct())
                            {
                                textY += 15;
                                e.Graphics.DrawString($"({nrml.X:R}, {nrml.Y:R}, {nrml.Z:R})", font, Brushes.CadetBlue, pt + new SizeF(0, textY), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                            }
                        }

                        if (Program.Settings.ShowTextures)
                        {
                            var textureInfs = Program.Settings.Faces.SelectMany(f => f.Vertices
                                .Where(v => v.Location == vertex && v.Texture != null)
                                .Select(v => new { VertexInfo = v, Selected = Program.Settings.SelectedFaces.Contains(f) })).ToArray();
                            foreach (var gr in textureInfs.GroupBy(v => v.VertexInfo.Texture.Value))
                            {
                                textY += 15;
                                e.Graphics.DrawString($"({gr.Key.X:R}, {gr.Key.Y:R})", gr.Any(inf => inf.Selected) ? fontBold : font, Brushes.ForestGreen, pt + new SizeF(0, textY), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                            }
                        }
                    }

            // Highlighted vertex
            if (_highlightVertex != null)
                e.Graphics.DrawEllipse(new Pen(Color.Silver, 2f), new RectangleF(trP(_highlightVertex.Value).ToPointF() - new SizeF(_selectionSize.Width, _selectionSize.Height), tm(_selectionSize, 2)));

            // Selected faces
            if (Program.Settings.IsFaceSelected)
                foreach (var face in Program.Settings.SelectedFaces)
                    e.Graphics.FillPolygon(
                        brush: new SolidBrush(Color.FromArgb(64, Color.Navy)),
                        points: face.Locations.Select(v => trP(v).ToPointF()).ToArray());

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
            (p.X - _actualShowingRect.Left) / _actualShowingRect.Width * ClientSize.Width,
            (p.Z - _actualShowingRect.Top) / _actualShowingRect.Height * ClientSize.Height);

        private Pt revTrP(double screenX, double screenY, Pt? orig = null) => new Pt(
            screenX / ClientSize.Width * _actualShowingRect.Width + _actualShowingRect.Left,
            orig?.Y ?? 0,
            screenY / ClientSize.Height * _actualShowingRect.Height + _actualShowingRect.Top);

        private void resize(object sender, EventArgs e)
        {
            recalculateBounds();
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
