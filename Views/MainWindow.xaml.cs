using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DiagramApp.Models;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace DiagramApp.Views;

public partial class MainWindow : Window
{
    private const double NodeWidth = 170;
    private const double NodeHeight = 92;
    private const double MinWorkspaceWidth = 2500;
    private const double MinWorkspaceHeight = 1600;
    private const double WorkspaceGrowWidth = 1200;
    private const double WorkspaceGrowHeight = 900;
    private const double WorkspacePadding = 260;
    private const double GridSize = 24;

    private const int DragRenderIntervalMs = 16;
    private const double DragRenderMinDistance = 1.8;
    private const string ProjectFileFilter = "Diagram Project (*.diagram.json)|*.diagram.json|JSON (*.json)|*.json|All files (*.*)|*.*";
    private const string PngFileFilter = "PNG Image (*.png)|*.png";

    private static readonly IReadOnlyDictionary<string, ThemePalette> ThemePalettes = new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ocean"] = new(Color.FromRgb(11, 18, 32), Color.FromRgb(18, 26, 47), Color.FromRgb(24, 37, 59), Color.FromRgb(23, 35, 57), Color.FromRgb(13, 21, 39), Color.FromRgb(34, 48, 74), Color.FromRgb(14, 22, 41), Color.FromRgb(241, 245, 249), Color.FromRgb(226, 232, 240), Color.FromRgb(30, 41, 59), Color.FromRgb(15, 23, 42), Color.FromRgb(59, 130, 246), Color.FromRgb(34, 197, 94), Color.FromRgb(251, 191, 36), Color.FromRgb(148, 163, 184), Color.FromRgb(14, 165, 233), Color.FromRgb(51, 65, 85), Color.FromRgb(127, 29, 29)),
        ["Forest"] = new(Color.FromRgb(9, 23, 20), Color.FromRgb(16, 43, 36), Color.FromRgb(19, 49, 42), Color.FromRgb(22, 56, 48), Color.FromRgb(11, 35, 31), Color.FromRgb(33, 78, 66), Color.FromRgb(16, 43, 36), Color.FromRgb(236, 253, 245), Color.FromRgb(220, 252, 231), Color.FromRgb(16, 67, 56), Color.FromRgb(6, 39, 32), Color.FromRgb(16, 185, 129), Color.FromRgb(74, 222, 128), Color.FromRgb(250, 204, 21), Color.FromRgb(167, 243, 208), Color.FromRgb(45, 212, 191), Color.FromRgb(21, 128, 61), Color.FromRgb(127, 29, 29)),
        ["Sunset"] = new(Color.FromRgb(36, 18, 18), Color.FromRgb(58, 26, 39), Color.FromRgb(61, 27, 37), Color.FromRgb(72, 31, 44), Color.FromRgb(46, 20, 31), Color.FromRgb(117, 58, 79), Color.FromRgb(70, 30, 45), Color.FromRgb(255, 241, 242), Color.FromRgb(255, 228, 230), Color.FromRgb(136, 50, 67), Color.FromRgb(81, 28, 43), Color.FromRgb(251, 113, 133), Color.FromRgb(253, 164, 175), Color.FromRgb(251, 191, 36), Color.FromRgb(254, 205, 211), Color.FromRgb(244, 114, 182), Color.FromRgb(136, 19, 55), Color.FromRgb(127, 29, 29))
    };

    private readonly DiagramModel _diagram = new();
    private readonly JsonSerializerOptions _jsonWriteOptions = new() { WriteIndented = true };
    private readonly JsonSerializerOptions _jsonCompactOptions = new() { WriteIndented = false };
    private readonly List<DiagramProjectFile> _history = [];

    private readonly SolidColorBrush _edgeBrush = new(Color.FromRgb(148, 163, 184));
    private readonly SolidColorBrush _nodeHeaderBrush = new(Color.FromRgb(148, 163, 184));
    private readonly SolidColorBrush _nodeTitleBrush = new(Color.FromRgb(241, 245, 249));
    private readonly SolidColorBrush _nodeBorderBrush = new(Color.FromRgb(59, 130, 246));
    private readonly SolidColorBrush _nodeSelectedBrush = new(Color.FromRgb(34, 197, 94));
    private readonly SolidColorBrush _nodePendingBrush = new(Color.FromRgb(251, 191, 36));
    private readonly SolidColorBrush _nodeShadowBrush = new(Color.FromRgb(8, 47, 73));

    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(20) };

    private DiagramNode? _dragNode;
    private Point _dragOffset;
    private bool _isDragging;
    private bool _dragChanged;
    private DateTime _lastDragRenderAtUtc = DateTime.MinValue;
    private Point _lastRenderedDragPointer;

    private bool _isPanning;
    private Point _panStartPointer;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    private Point _shapePaletteDragStart;

    private string? _selectedNodeId;
    private string? _pendingConnectionSourceId;
    private string? _inlineEditNodeId;
    private string? _currentProjectFilePath;
    private string? _savedStateSignature;

    private NodeClipboardData? _clipboardNode;
    private int _pasteOffsetStep;

    private bool _isRefreshingSelectors;
    private bool _isProgrammaticThemeChange;
    private bool _isProgrammaticZoomChange;
    private bool _suppressStateTracking;
    private bool _isDirty;
    private bool _lightweightRender;
    private bool _autosaveInProgress;

    private int _historyIndex = -1;
    private int _renderCount;
    private double _renderMsTotal;
    private double _zoomScale = 1.0;
    private double _workspaceWidth = MinWorkspaceWidth;
    private double _workspaceHeight = MinWorkspaceHeight;

    private Color _nodeCardStartColor = Color.FromRgb(30, 41, 59);
    private Color _nodeCardEndColor = Color.FromRgb(15, 23, 42);

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;

        ApplyWorkspaceSize(_workspaceWidth, _workspaceHeight);
        InitializeAutosave();

        SetThemeSelection("Ocean");
        SetDefaultShapeSelection(NodeShapeType.Rectangle);
        SetZoomScale(1.0);

        RefreshNodeSelectors();
        SelectNode(null, reRender: false);
        RenderDiagram();

        InitializeHistoryFromCurrentState();
        SetSavedSignatureToCurrent();

        StatusTextBlock.Text = "Ready. Shortcuts: Ctrl+S, Ctrl+O, Ctrl+N, Ctrl+Z, Ctrl+Y, Ctrl+C, Ctrl+V, Ctrl+D, Delete";
        AutosaveStatusTextBlock.Text = "Autosave: On (20s)";
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!EnsureCanDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        DisposeRuntimeResources();
    }

    private void DisposeRuntimeResources()
    {
        _autosaveTimer.Tick -= AutosaveTimer_Tick;
        _autosaveTimer.Stop();
        if (_isPanning)
        {
            EndPan();
        }
    }

    private void InitializeAutosave()
    {
        _autosaveTimer.Tick += AutosaveTimer_Tick;
        _autosaveTimer.Start();
    }

    private void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_autosaveInProgress || _suppressStateTracking || !_isDirty)
        {
            return;
        }

        try
        {
            _autosaveInProgress = true;
            var autosavePath = GetAutosaveFilePath();
            Directory.CreateDirectory(IOPath.GetDirectoryName(autosavePath)!);

            var json = JsonSerializer.Serialize(CaptureCurrentProject(), _jsonWriteOptions);
            File.WriteAllText(autosavePath, json);
            AutosaveStatusTextBlock.Text = $"Autosave: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            AutosaveStatusTextBlock.Text = $"Autosave error: {ex.Message}";
        }
        finally
        {
            _autosaveInProgress = false;
        }
    }

    private string GetAutosaveFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            return _currentProjectFilePath + ".autosave";
        }

        var autosaveDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiagramStudio");
        return IOPath.Combine(autosaveDir, "unsaved.autosave.diagram.json");
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (ctrlPressed)
        {
            switch (e.Key)
            {
                case Key.S when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                    SaveProjectAs();
                    e.Handled = true;
                    return;
                case Key.S:
                    SaveCurrentProject();
                    e.Handled = true;
                    return;
                case Key.O:
                    OpenProjectButton_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.N:
                    NewProjectButton_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.Z:
                    UndoHistory();
                    e.Handled = true;
                    return;
                case Key.Y:
                    RedoHistory();
                    e.Handled = true;
                    return;
                case Key.C when !IsTextInputFocused():
                    CopySelectedNode();
                    e.Handled = true;
                    return;
                case Key.V when !IsTextInputFocused():
                    PasteClipboardNode();
                    e.Handled = true;
                    return;
                case Key.D when !IsTextInputFocused():
                    DuplicateSelectedNode();
                    e.Handled = true;
                    return;
                case Key.E when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                    ExportToPng();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.F2 && _selectedNodeId is not null)
        {
            BeginInlineEdit(_selectedNodeId);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && !IsTextInputFocused())
        {
            DeleteSelectedNodeButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelPendingActions();
            e.Handled = true;
        }
    }

    private void CancelPendingActions()
    {
        if (_inlineEditNodeId is not null)
        {
            _inlineEditNodeId = null;
            RenderDiagram();
        }

        if (_pendingConnectionSourceId is not null)
        {
            _pendingConnectionSourceId = null;
            RenderDiagram();
            StatusTextBlock.Text = "Connection selection canceled.";
        }
    }

    private static bool IsTextInputFocused()
    {
        var focused = Keyboard.FocusedElement;
        return focused is TextBox || focused is ComboBox;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveCurrentProject();
    private void SaveAsButton_Click(object sender, RoutedEventArgs e) => SaveProjectAs();
    private void UndoButton_Click(object sender, RoutedEventArgs e) => UndoHistory();
    private void RedoButton_Click(object sender, RoutedEventArgs e) => RedoHistory();
    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => SetZoomScale(_zoomScale - 0.1);
    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => SetZoomScale(_zoomScale + 0.1);
    private void FitViewButton_Click(object sender, RoutedEventArgs e) => FitToView();
    private void ExportPngButton_Click(object sender, RoutedEventArgs e) => ExportToPng();

    private void SnapToGridCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressStateTracking || StatusTextBlock is null)
        {
            return;
        }

        var enabled = sender switch
        {
            CheckBox checkBox => checkBox.IsChecked == true,
            MenuItem menuItem => menuItem.IsChecked,
            _ => SnapToGridCheckBox?.IsChecked == true
        };

        StatusTextBlock.Text = enabled ? "Snap-to-grid enabled." : "Snap-to-grid disabled.";
    }

    private bool IsSnapEnabled() => SnapToGridCheckBox?.IsChecked == true;

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isProgrammaticZoomChange || ZoomSlider is null)
        {
            return;
        }

        SetZoomScale(ZoomSlider.Value / 100.0, syncSlider: false);
    }

    private void WorkspaceScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var oldScale = _zoomScale;
        var step = e.Delta > 0 ? 0.1 : -0.1;
        var nextScale = Math.Clamp(oldScale + step, 0.25, 2.5);
        if (Math.Abs(nextScale - oldScale) < 0.0001)
        {
            return;
        }

        var pointer = e.GetPosition(WorkspaceScrollViewer);
        var absoluteX = (WorkspaceScrollViewer.HorizontalOffset + pointer.X) / oldScale;
        var absoluteY = (WorkspaceScrollViewer.VerticalOffset + pointer.Y) / oldScale;

        SetZoomScale(nextScale);
        WorkspaceScrollViewer.ScrollToHorizontalOffset(Math.Max(0, absoluteX * nextScale - pointer.X));
        WorkspaceScrollViewer.ScrollToVerticalOffset(Math.Max(0, absoluteY * nextScale - pointer.Y));
        e.Handled = true;
    }

    private void WorkspaceScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var canPan = e.MiddleButton == MouseButtonState.Pressed ||
                     (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space));

        if (!canPan)
        {
            return;
        }

        CommitInlineEditIfAny();
        _isPanning = true;
        _panStartPointer = e.GetPosition(WorkspaceScrollViewer);
        _panStartHorizontalOffset = WorkspaceScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = WorkspaceScrollViewer.VerticalOffset;

        Mouse.OverrideCursor = Cursors.SizeAll;
        WorkspaceScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void WorkspaceScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var pointer = e.GetPosition(WorkspaceScrollViewer);
        var dx = pointer.X - _panStartPointer.X;
        var dy = pointer.Y - _panStartPointer.Y;

        WorkspaceScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _panStartHorizontalOffset - dx));
        WorkspaceScrollViewer.ScrollToVerticalOffset(Math.Max(0, _panStartVerticalOffset - dy));

        e.Handled = true;
    }

    private void WorkspaceScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        if (e.MiddleButton == MouseButtonState.Released && e.LeftButton == MouseButtonState.Released)
        {
            EndPan();
            e.Handled = true;
        }
    }

    private void WorkspaceScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isPanning && e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released)
        {
            EndPan();
        }
    }

    private void EndPan()
    {
        _isPanning = false;
        Mouse.OverrideCursor = null;
        WorkspaceScrollViewer.ReleaseMouseCapture();
    }

    private void ShapePaletteListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _shapePaletteDragStart = e.GetPosition(ShapePaletteListBox);
            return;
        }

        var current = e.GetPosition(ShapePaletteListBox);
        var delta = current - _shapePaletteDragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var item = FindAncestor<ListBoxItem>(source);
        var shapeTag = item?.Tag as string;
        if (string.IsNullOrWhiteSpace(shapeTag))
        {
            return;
        }

        DragDrop.DoDragDrop(item, new DataObject(DataFormats.StringFormat, shapeTag), DragDropEffects.Copy);
    }

    private void NodeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingSelectors)
        {
            return;
        }

        EnsureDistinctSelectorValues(sender);
        UpdateConnectUiState();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySelectedTheme();
        UpdateThemeMenuSelection(GetSelectedThemeKey());
        RenderDiagram();

        if (!_isProgrammaticThemeChange && !_suppressStateTracking)
        {
            CommitState("Theme updated.");
        }
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string themeKey)
        {
            return;
        }

        SetThemeSelection(themeKey);

        if (!_suppressStateTracking)
        {
            CommitState("Theme updated.");
        }
    }
    private void NewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanDiscardChanges())
        {
            return;
        }

        _diagram.Clear();
        _pendingConnectionSourceId = null;
        _selectedNodeId = null;
        _inlineEditNodeId = null;
        _currentProjectFilePath = null;
        _workspaceWidth = MinWorkspaceWidth;
        _workspaceHeight = MinWorkspaceHeight;
        ApplyWorkspaceSize(_workspaceWidth, _workspaceHeight);

        RefreshNodeSelectors();
        SelectNode(null, reRender: false);
        RenderDiagram();

        InitializeHistoryFromCurrentState();
        SetSavedSignatureToCurrent();
        StatusTextBlock.Text = "New project created.";
    }

    private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = ProjectFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadProjectFromPath(dialog.FileName);
    }

    private void DiagramCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DiagramCanvas_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            return;
        }

        var shapeText = e.Data.GetData(DataFormats.StringFormat) as string;
        var shapeType = ParseNodeShapeType(shapeText);

        var pointer = ToWorkspacePoint(e.GetPosition(DiagramCanvas));
        var x = Math.Max(10, pointer.X - NodeWidth / 2);
        var y = Math.Max(10, pointer.Y - NodeHeight / 2);

        if (IsSnapEnabled())
        {
            x = SnapCoordinate(x);
            y = SnapCoordinate(y);
        }

        var title = shapeType switch
        {
            NodeShapeType.Ellipse => "Ellipse Node",
            NodeShapeType.DecisionDiamond => "Decision Node",
            _ => "Rectangle Node"
        };

        var node = _diagram.AddNode(title, x, y, shapeType, description: string.Empty);
        NormalizeNodeToWorkspace(node);

        RefreshNodeSelectors();
        SelectNode(node.Id, reRender: false);
        RenderDiagram();
        SelectNewNodeForConnection(node.Id);
        CommitState($"Node added ({shapeType}).");
    }

    private void AddNodeButton_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineEditIfAny();

        var title = string.IsNullOrWhiteSpace(NodeTitleTextBox.Text) ? "New Node" : NodeTitleTextBox.Text.Trim();
        var description = NormalizeDescription(NodeDescriptionTextBox.Text);
        var shapeType = GetSelectedCreateShapeType();
        var pos = NextNodePosition();

        var node = _diagram.AddNode(title, pos.X, pos.Y, shapeType, description);
        NormalizeNodeToWorkspace(node);

        NodeTitleTextBox.Text = string.Empty;
        NodeDescriptionTextBox.Text = string.Empty;
        RefreshNodeSelectors();
        SelectNode(node.Id, reRender: false);
        RenderDiagram();

        SelectNewNodeForConnection(node.Id);
        CommitState($"{node.Id} added.");
    }

    private void RenameNodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNodeId is null)
        {
            StatusTextBlock.Text = "Select a node to rename.";
            return;
        }

        var node = _diagram.FindNode(_selectedNodeId);
        if (node is null)
        {
            SelectNode(null, reRender: true);
            return;
        }

        var newTitle = string.IsNullOrWhiteSpace(RenameNodeTextBox.Text) ? node.Title : RenameNodeTextBox.Text.Trim();
        var newDescription = NormalizeDescription(RenameNodeDescriptionTextBox.Text);
        if (string.Equals(node.Title, newTitle, StringComparison.Ordinal) &&
            string.Equals(node.Description, newDescription, StringComparison.Ordinal))
        {
            StatusTextBlock.Text = "Node details are already the same.";
            return;
        }

        node.Title = newTitle;
        node.Description = newDescription;
        RenderDiagram();
        UpdateSelectionPanel();
        CommitState($"{node.Id} details updated.");
    }

    private void DeleteSelectedNodeButton_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineEditIfAny();

        if (_selectedNodeId is null)
        {
            StatusTextBlock.Text = "Select a node first to delete.";
            return;
        }

        var nodeId = _selectedNodeId;
        if (!_diagram.RemoveNode(nodeId))
        {
            StatusTextBlock.Text = "Node could not be deleted.";
            return;
        }

        if (_pendingConnectionSourceId is not null && _pendingConnectionSourceId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
        {
            _pendingConnectionSourceId = null;
        }

        if (_inlineEditNodeId is not null && _inlineEditNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
        {
            _inlineEditNodeId = null;
        }

        SelectNode(null, reRender: false);
        RefreshNodeSelectors();
        RenderDiagram();
        CommitState($"Node deleted: {nodeId}");
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var fromId = FromNodeComboBox.SelectedItem as string;
        var toId = ToNodeComboBox.SelectedItem as string;

        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
        {
            StatusTextBlock.Text = "Select two nodes to create a connection.";
            return;
        }

        TryCreateConnection(fromId, toId);
    }

    private void RemoveEdgeButton_Click(object sender, RoutedEventArgs e)
    {
        var fromId = FromNodeComboBox.SelectedItem as string;
        var toId = ToNodeComboBox.SelectedItem as string;

        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
        {
            StatusTextBlock.Text = "Select a valid connection to remove.";
            return;
        }

        if (!_diagram.RemoveEdge(fromId, toId))
        {
            StatusTextBlock.Text = "Selected connection was not found.";
            UpdateConnectUiState();
            return;
        }

        RenderDiagram();
        UpdateConnectUiState();
        CommitState($"Connection removed: {fromId} -> {toId}");
    }

    private void AutoLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineEditIfAny();

        var nodes = _diagram.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (nodes.Count == 0)
        {
            StatusTextBlock.Text = "No nodes available for auto layout.";
            return;
        }

        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(nodes.Count)));
        var spacingX = NodeWidth + 84;
        var spacingY = NodeHeight + 82;

        for (var i = 0; i < nodes.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;

            var x = 80 + col * spacingX;
            var y = 80 + row * spacingY;

            if (IsSnapEnabled())
            {
                x = SnapCoordinate(x);
                y = SnapCoordinate(y);
            }

            nodes[i].X = x;
            nodes[i].Y = y;
            NormalizeNodeToWorkspace(nodes[i]);
        }

        RenderDiagram();
        CommitState("Auto layout applied.");
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineEditIfAny();

        if (_diagram.Nodes.Count == 0)
        {
            StatusTextBlock.Text = "Diagram is already empty.";
            return;
        }

        _diagram.Clear();
        _pendingConnectionSourceId = null;
        _inlineEditNodeId = null;
        _workspaceWidth = MinWorkspaceWidth;
        _workspaceHeight = MinWorkspaceHeight;
        ApplyWorkspaceSize(_workspaceWidth, _workspaceHeight);

        SelectNode(null, reRender: false);
        RefreshNodeSelectors();
        RenderDiagram();
        CommitState("Diagram cleared.");
    }


    private Rect CalculateDiagramBounds()
    {
        var nodes = _diagram.Nodes.ToList();
        if (nodes.Count == 0)
        {
            return Rect.Empty;
        }

        var minX = nodes.Min(n => n.X);
        var minY = nodes.Min(n => n.Y);
        var maxX = nodes.Max(n => n.X + NodeWidth);
        var maxY = nodes.Max(n => n.Y + NodeHeight);

        const double padding = 80;
        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(_workspaceWidth, maxX + padding);
        maxY = Math.Min(_workspaceHeight, maxY + padding);

        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private bool EnsureCanDiscardChanges()
    {
        if (!_isDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            "There are unsaved changes. Do you want to save before continuing?",
            "ST Diagram Studio",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        return result != MessageBoxResult.Yes || SaveCurrentProject();
    }

    private void NormalizeNodeToWorkspace(DiagramNode node)
    {
        node.X = Math.Max(10, node.X);
        node.Y = Math.Max(10, node.Y);

        if (IsSnapEnabled())
        {
            node.X = SnapCoordinate(node.X);
            node.Y = SnapCoordinate(node.Y);
        }

        EnsureWorkspaceForNode(node);
    }

    private void EnsureWorkspaceForNode(DiagramNode node)
    {
        EnsureWorkspaceForBounds(node.X, node.Y, NodeWidth, NodeHeight);
    }

    private void EnsureWorkspaceForBounds(double x, double y, double width, double height)
    {
        var resized = false;

        while (x + width + WorkspacePadding > _workspaceWidth)
        {
            _workspaceWidth += WorkspaceGrowWidth;
            resized = true;
        }

        while (y + height + WorkspacePadding > _workspaceHeight)
        {
            _workspaceHeight += WorkspaceGrowHeight;
            resized = true;
        }

        if (resized)
        {
            ApplyWorkspaceSize(_workspaceWidth, _workspaceHeight);
        }
    }

    private void ApplyWorkspaceSize(double width, double height)
    {
        WorkspaceSurface.Width = width;
        WorkspaceSurface.Height = height;
        WorkspaceGridRect.Width = width;
        WorkspaceGridRect.Height = height;
        DiagramCanvas.Width = width;
        DiagramCanvas.Height = height;
    }


    private void SetDefaultShapeSelection(NodeShapeType shapeType)
    {
        var selected = NodeShapeComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, shapeType.ToString(), StringComparison.OrdinalIgnoreCase));

        NodeShapeComboBox.SelectedItem = selected ?? NodeShapeComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private NodeShapeType GetSelectedCreateShapeType()
    {
        var tag = (NodeShapeComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        return ParseNodeShapeType(tag);
    }

    private static NodeShapeType ParseNodeShapeType(string? shapeTag)
    {
        if (string.IsNullOrWhiteSpace(shapeTag))
        {
            return NodeShapeType.Rectangle;
        }

        return Enum.TryParse<NodeShapeType>(shapeTag, ignoreCase: true, out var shape)
            ? shape
            : NodeShapeType.Rectangle;
    }

    private static string NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    }

    private void SetZoomScale(double scale, bool syncSlider = true)
    {
        _zoomScale = Math.Clamp(scale, 0.25, 2.5);
        if (WorkspaceScaleTransform is null)
        {
            return;
        }

        WorkspaceScaleTransform.ScaleX = _zoomScale;
        WorkspaceScaleTransform.ScaleY = _zoomScale;

        if (syncSlider && ZoomSlider is not null)
        {
            _isProgrammaticZoomChange = true;
            ZoomSlider.Value = _zoomScale * 100.0;
            _isProgrammaticZoomChange = false;
        }

        if (ZoomTextBlock is not null)
        {
            ZoomTextBlock.Text = $"{_zoomScale * 100:0}%";
        }
    }

    private void FitToView()
    {
        if (WorkspaceScrollViewer.ViewportWidth <= 0 || WorkspaceScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var fit = Math.Clamp(
            Math.Min(WorkspaceScrollViewer.ViewportWidth / _workspaceWidth, WorkspaceScrollViewer.ViewportHeight / _workspaceHeight),
            0.25,
            2.5);

        SetZoomScale(fit);
        WorkspaceScrollViewer.ScrollToHorizontalOffset(0);
        WorkspaceScrollViewer.ScrollToVerticalOffset(0);
    }

    private Point NextNodePosition()
    {
        var viewport = GetVisibleWorkspaceBounds();

        var minX = Math.Max(10, viewport.X + 20);
        var minY = Math.Max(10, viewport.Y + 20);
        var maxX = Math.Max(minX, Math.Min(_workspaceWidth - NodeWidth - 10, viewport.Right - NodeWidth - 20));
        var maxY = Math.Max(minY, Math.Min(_workspaceHeight - NodeHeight - 10, viewport.Bottom - NodeHeight - 20));

        var centerX = minX + (maxX - minX) / 2.0;
        var centerY = minY + (maxY - minY) / 2.0;

        var offsets = new[]
        {
            new Point(0, 0),
            new Point(1, 0),
            new Point(-1, 0),
            new Point(0, 1),
            new Point(0, -1),
            new Point(1, 1),
            new Point(-1, 1),
            new Point(1, -1),
            new Point(-1, -1)
        };

        var offset = offsets[_diagram.Nodes.Count % offsets.Length];
        var x = centerX + offset.X * 32;
        var y = centerY + offset.Y * 26;

        if (IsSnapEnabled())
        {
            x = SnapCoordinate(x);
            y = SnapCoordinate(y);
        }

        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);
        return new Point(x, y);
    }

    private Rect GetVisibleWorkspaceBounds()
    {
        var scale = Math.Max(0.001, _zoomScale);
        var viewportWidth = WorkspaceScrollViewer.ViewportWidth / scale;
        var viewportHeight = WorkspaceScrollViewer.ViewportHeight / scale;

        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            return new Rect(0, 0, _workspaceWidth, _workspaceHeight);
        }

        var x = WorkspaceScrollViewer.HorizontalOffset / scale;
        var y = WorkspaceScrollViewer.VerticalOffset / scale;

        x = Math.Clamp(x, 0, Math.Max(0, _workspaceWidth - viewportWidth));
        y = Math.Clamp(y, 0, Math.Max(0, _workspaceHeight - viewportHeight));

        return new Rect(x, y, Math.Min(viewportWidth, _workspaceWidth), Math.Min(viewportHeight, _workspaceHeight));
    }

    private double SnapCoordinate(double value)
    {
        return Math.Round(value / GridSize) * GridSize;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private Point GetWorkspacePointerFromMouse(MouseEventArgs e)
    {
        return ToWorkspacePoint(e.GetPosition(DiagramCanvas));
    }

    private Point ToWorkspacePoint(Point point)
    {
        var scale = Math.Max(0.001, _zoomScale);
        return new Point(point.X / scale, point.Y / scale);
    }

    private Point GetNodeCenter(DiagramNode node)
    {
        return new Point(node.X + NodeWidth / 2, node.Y + NodeHeight / 2);
    }

    private Point GetConnectorPoint(DiagramNode node, Point targetPoint)
    {
        var center = GetNodeCenter(node);
        var dx = targetPoint.X - center.X;
        var dy = targetPoint.Y - center.Y;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            return center;
        }

        var halfW = NodeWidth / 2;
        var halfH = NodeHeight / 2;

        return node.ShapeType switch
        {
            NodeShapeType.Ellipse => GetEllipseConnector(center, dx, dy, halfW, halfH),
            NodeShapeType.DecisionDiamond => GetDiamondConnector(center, dx, dy, halfW, halfH),
            _ => GetRectangleConnector(center, dx, dy, halfW, halfH)
        };
    }

    private static Point GetRectangleConnector(Point center, double dx, double dy, double halfW, double halfH)
    {
        var factor = 1.0 / Math.Max(Math.Abs(dx) / halfW, Math.Abs(dy) / halfH);
        return new Point(center.X + dx * factor, center.Y + dy * factor);
    }

    private static Point GetEllipseConnector(Point center, double dx, double dy, double halfW, double halfH)
    {
        var denominator = Math.Sqrt((dx * dx) / (halfW * halfW) + (dy * dy) / (halfH * halfH));
        var factor = denominator < 0.001 ? 1 : 1 / denominator;
        return new Point(center.X + dx * factor, center.Y + dy * factor);
    }

    private static Point GetDiamondConnector(Point center, double dx, double dy, double halfW, double halfH)
    {
        var denominator = (Math.Abs(dx) / halfW) + (Math.Abs(dy) / halfH);
        var factor = denominator < 0.001 ? 1 : 1 / denominator;
        return new Point(center.X + dx * factor, center.Y + dy * factor);
    }

    private sealed record NodeClipboardData(string Title, string Description, NodeShapeType ShapeType, double X, double Y);

    private sealed record ThemePalette(
        Color GradientStart,
        Color GradientEnd,
        Color SidePanel,
        Color Toolbar,
        Color CanvasPanel,
        Color CanvasBorder,
        Color InputBackground,
        Color InputForeground,
        Color SelectorBackground,
        Color NodeCardStart,
        Color NodeCardEnd,
        Color NodeBorder,
        Color NodeSelected,
        Color NodePending,
        Color Edge,
        Color Action,
        Color Utility,
        Color Danger);
}
