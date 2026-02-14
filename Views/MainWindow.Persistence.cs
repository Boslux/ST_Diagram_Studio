using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiagramApp.Models;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace DiagramApp.Views;

public partial class MainWindow
{
    private bool SaveCurrentProject()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            return SaveProjectAs();
        }

        return SaveProjectToPath(_currentProjectFilePath);
    }

    private bool SaveProjectAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = ProjectFileFilter,
            DefaultExt = ".diagram.json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = string.IsNullOrWhiteSpace(_currentProjectFilePath) ? "diagram-project.diagram.json" : IOPath.GetFileName(_currentProjectFilePath)
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        return SaveProjectToPath(dialog.FileName);
    }

    private bool SaveProjectToPath(string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(CaptureCurrentProject(), _jsonWriteOptions);
            File.WriteAllText(path, json);

            _currentProjectFilePath = path;
            SetSavedSignatureToCurrent();
            StatusTextBlock.Text = $"Project saved: {IOPath.GetFileName(path)}";
            AutosaveStatusTextBlock.Text = "Autosave: synced";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save error: {ex.Message}", "ST Diagram Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool LoadProjectFromPath(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var project = JsonSerializer.Deserialize<DiagramProjectFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (project is null)
            {
                throw new InvalidDataException("Project file could not be read.");
            }

            ApplySnapshot(project);
            _currentProjectFilePath = path;

            InitializeHistoryFromCurrentState();
            SetSavedSignatureToCurrent();
            StatusTextBlock.Text = $"Project loaded: {IOPath.GetFileName(path)}";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Open error: {ex.Message}", "ST Diagram Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void InitializeHistoryFromCurrentState()
    {
        _history.Clear();
        _history.Add(CloneProject(CaptureCurrentProject()));
        _historyIndex = 0;
        UpdateUndoRedoUi();
    }

    private void PushHistoryState(bool force = false)
    {
        var snapshot = CaptureCurrentProject();

        if (!force && _historyIndex >= 0)
        {
            var currentSignature = ComputeSignature(_history[_historyIndex]);
            var nextSignature = ComputeSignature(snapshot);
            if (string.Equals(currentSignature, nextSignature, StringComparison.Ordinal))
            {
                UpdateUndoRedoUi();
                return;
            }
        }

        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(CloneProject(snapshot));
        if (_history.Count > 120)
        {
            _history.RemoveAt(0);
        }

        _historyIndex = _history.Count - 1;
        UpdateUndoRedoUi();
    }

    private void UndoHistory()
    {
        if (_historyIndex <= 0)
        {
            return;
        }

        _historyIndex--;
        ApplySnapshot(_history[_historyIndex]);
        UpdateUndoRedoUi();
        RefreshDirtyState("Undone.");
    }

    private void RedoHistory()
    {
        if (_historyIndex >= _history.Count - 1)
        {
            return;
        }

        _historyIndex++;
        ApplySnapshot(_history[_historyIndex]);
        UpdateUndoRedoUi();
        RefreshDirtyState("Redone.");
    }

    private void ApplySnapshot(DiagramProjectFile snapshot)
    {
        _suppressStateTracking = true;

        _workspaceWidth = Math.Max(MinWorkspaceWidth, snapshot.CanvasWidth);
        _workspaceHeight = Math.Max(MinWorkspaceHeight, snapshot.CanvasHeight);
        ApplyWorkspaceSize(_workspaceWidth, _workspaceHeight);

        _diagram.ImportProject(CloneProject(snapshot));

        foreach (var node in _diagram.Nodes)
        {
            NormalizeNodeToWorkspace(node);
        }

        _pendingConnectionSourceId = null;
        _inlineEditNodeId = null;
        if (_selectedNodeId is not null && _diagram.FindNode(_selectedNodeId) is null)
        {
            _selectedNodeId = null;
        }

        SetThemeSelection(snapshot.ThemeKey);
        RefreshNodeSelectors();
        UpdateSelectionPanel();
        RenderDiagram();

        _suppressStateTracking = false;
    }
    private void UpdateUndoRedoUi()
    {
        var canUndo = _historyIndex > 0;
        var canRedo = _historyIndex >= 0 && _historyIndex < _history.Count - 1;

        UndoButton.IsEnabled = canUndo;
        UndoButton.Opacity = canUndo ? 1 : 0.55;
        RedoButton.IsEnabled = canRedo;
        RedoButton.Opacity = canRedo ? 1 : 0.55;
    }

    private void CommitState(string statusText)
    {
        if (_suppressStateTracking)
        {
            return;
        }

        PushHistoryState();
        RefreshDirtyState(statusText);
    }

    private void SetSavedSignatureToCurrent()
    {
        _savedStateSignature = ComputeSignature(CaptureCurrentProject());
        _isDirty = false;
        UpdateWindowTitleAndProjectPath();
    }

    private void RefreshDirtyState(string? statusText = null)
    {
        var currentSig = ComputeSignature(CaptureCurrentProject());
        _isDirty = _savedStateSignature is null || !string.Equals(_savedStateSignature, currentSig, StringComparison.Ordinal);
        UpdateWindowTitleAndProjectPath();

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            StatusTextBlock.Text = statusText;
        }
    }

    private void UpdateWindowTitleAndProjectPath()
    {
        var projectName = string.IsNullOrWhiteSpace(_currentProjectFilePath)
            ? "Untitled"
            : IOPath.GetFileNameWithoutExtension(_currentProjectFilePath);

        Title = _isDirty ? $"{projectName} * - ST Diagram Studio" : $"{projectName} - ST Diagram Studio";
        ProjectPathTextBlock.Text = string.IsNullOrWhiteSpace(_currentProjectFilePath)
            ? "Project: Untitled (not saved yet)"
            : $"Project: {_currentProjectFilePath}";
    }

    private DiagramProjectFile CaptureCurrentProject()
    {
        var project = _diagram.ExportProject(GetSelectedThemeKey());
        project.CanvasWidth = _workspaceWidth;
        project.CanvasHeight = _workspaceHeight;
        return project;
    }

    private DiagramProjectFile CloneProject(DiagramProjectFile source)
    {
        var json = JsonSerializer.Serialize(source, _jsonCompactOptions);
        return JsonSerializer.Deserialize<DiagramProjectFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DiagramProjectFile();
    }

    private string ComputeSignature(DiagramProjectFile project)
    {
        return JsonSerializer.Serialize(project, _jsonCompactOptions);
    }

    private void ExportToPng()
    {
        if (_diagram.Nodes.Count == 0)
        {
            StatusTextBlock.Text = "Diagram is empty for export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = PngFileFilter,
            AddExtension = true,
            DefaultExt = ".png",
            FileName = "diagram-export.png"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var bounds = CalculateDiagramBounds();
        if (bounds.IsEmpty || bounds.Width <= 1 || bounds.Height <= 1)
        {
            StatusTextBlock.Text = "Could not calculate export area.";
            return;
        }

        const int maxDimension = 8192;
        var scale = Math.Min(1.0, maxDimension / Math.Max(bounds.Width, bounds.Height));
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            var brush = new VisualBrush(WorkspaceSurface)
            {
                Viewbox = bounds,
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            dc.DrawRectangle(brush, null, new Rect(0, 0, pixelWidth, pixelHeight));
        }

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(drawingVisual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.Create(dialog.FileName);
        encoder.Save(fs);

        StatusTextBlock.Text = $"PNG export completed: {IOPath.GetFileName(dialog.FileName)}";
    }

}
