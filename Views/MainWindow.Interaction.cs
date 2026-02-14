using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiagramApp.Models;

namespace DiagramApp.Views;

public partial class MainWindow
{
    private void RefreshNodeSelectors()
    {
        var ids = _diagram.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase).Select(n => n.Id).ToList();
        var prevFrom = FromNodeComboBox.SelectedItem as string;
        var prevTo = ToNodeComboBox.SelectedItem as string;

        _isRefreshingSelectors = true;
        FromNodeComboBox.ItemsSource = ids;
        ToNodeComboBox.ItemsSource = ids;

        var from = prevFrom is not null && ids.Contains(prevFrom) ? prevFrom : ids.FirstOrDefault();
        var to = prevTo is not null && ids.Contains(prevTo) ? prevTo : null;

        if (from is not null && to is not null && from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            to = null;
        }

        from ??= ids.FirstOrDefault();
        to ??= FirstDifferentNodeId(ids, from);

        FromNodeComboBox.SelectedItem = from;
        ToNodeComboBox.SelectedItem = to;
        _isRefreshingSelectors = false;

        if (_pendingConnectionSourceId is not null && !ids.Contains(_pendingConnectionSourceId))
        {
            _pendingConnectionSourceId = null;
        }

        if (_selectedNodeId is not null && !ids.Contains(_selectedNodeId))
        {
            _selectedNodeId = null;
        }

        if (_inlineEditNodeId is not null && !ids.Contains(_inlineEditNodeId))
        {
            _inlineEditNodeId = null;
        }

        UpdateSelectionPanel();
        UpdateConnectUiState();
    }

    private void SelectNode(string? nodeId, bool reRender)
    {
        if (nodeId is not null && _diagram.FindNode(nodeId) is null)
        {
            nodeId = null;
        }

        var changed = !string.Equals(_selectedNodeId, nodeId, StringComparison.OrdinalIgnoreCase);
        _selectedNodeId = nodeId;
        UpdateSelectionPanel();

        if (changed && reRender)
        {
            RenderDiagram();
        }
    }

    private void UpdateSelectionPanel()
    {
        if (_selectedNodeId is null)
        {
            SelectedNodeTextBlock.Text = "(No node selected)";
            RenameNodeTextBox.Text = string.Empty;
            RenameNodeButton.IsEnabled = false;
            RenameNodeButton.Opacity = 0.55;
            DeleteSelectedNodeButton.IsEnabled = false;
            DeleteSelectedNodeButton.Opacity = 0.55;
            return;
        }

        var node = _diagram.FindNode(_selectedNodeId);
        if (node is null)
        {
            _selectedNodeId = null;
            UpdateSelectionPanel();
            return;
        }

        SelectedNodeTextBlock.Text = $"{node.Id} - {node.Title} ({node.ShapeType})";
        RenameNodeTextBox.Text = node.Title;
        RenameNodeButton.IsEnabled = true;
        RenameNodeButton.Opacity = 1;
        DeleteSelectedNodeButton.IsEnabled = true;
        DeleteSelectedNodeButton.Opacity = 1;
    }

    private void EnsureDistinctSelectorValues(object? changedSelector)
    {
        var from = FromNodeComboBox.SelectedItem as string;
        var to = ToNodeComboBox.SelectedItem as string;
        if (from is null || to is null || !from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ids = _diagram.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase).Select(n => n.Id).ToList();

        _isRefreshingSelectors = true;
        if (ReferenceEquals(changedSelector, FromNodeComboBox))
        {
            ToNodeComboBox.SelectedItem = FirstDifferentNodeId(ids, from);
        }
        else
        {
            FromNodeComboBox.SelectedItem = FirstDifferentNodeId(ids, to);
        }
        _isRefreshingSelectors = false;
    }

    private void UpdateConnectUiState()
    {
        var fromId = FromNodeComboBox.SelectedItem as string;
        var toId = ToNodeComboBox.SelectedItem as string;
        var hasValidSelection = !string.IsNullOrWhiteSpace(fromId) &&
                                !string.IsNullOrWhiteSpace(toId) &&
                                !fromId.Equals(toId, StringComparison.OrdinalIgnoreCase);

        ConnectButton.IsEnabled = hasValidSelection;
        ConnectButton.Opacity = hasValidSelection ? 1 : 0.55;

        var canRemove = hasValidSelection && _diagram.HasEdge(fromId!, toId!);
        RemoveEdgeButton.IsEnabled = canRemove;
        RemoveEdgeButton.Opacity = canRemove ? 1 : 0.55;
    }

    private static string? FirstDifferentNodeId(IEnumerable<string> ids, string? fromId)
    {
        if (fromId is null)
        {
            return ids.FirstOrDefault();
        }

        return ids.FirstOrDefault(id => !id.Equals(fromId, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectNewNodeForConnection(string newNodeId)
    {
        var ids = _diagram.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase).Select(n => n.Id).ToList();
        if (ids.Count < 2)
        {
            UpdateConnectUiState();
            return;
        }

        _isRefreshingSelectors = true;
        ToNodeComboBox.SelectedItem = newNodeId;

        var selectedFrom = FromNodeComboBox.SelectedItem as string;
        if (selectedFrom is null || selectedFrom.Equals(newNodeId, StringComparison.OrdinalIgnoreCase))
        {
            FromNodeComboBox.SelectedItem = FirstDifferentNodeId(ids, newNodeId);
        }

        _isRefreshingSelectors = false;
        UpdateConnectUiState();
    }

    private void TryCreateConnection(string fromId, string toId)
    {
        if (_diagram.Connect(fromId, toId))
        {
            _pendingConnectionSourceId = null;
            RenderDiagram();
            UpdateConnectUiState();
            CommitState($"Connection created: {fromId} -> {toId}");
            return;
        }

        StatusTextBlock.Text = "Connection could not be created (duplicate, missing node, or same node).";
    }

    private bool ShouldRenderDuringDrag(Point pointer)
    {
        var now = DateTime.UtcNow;
        var movedDistance = (pointer - _lastRenderedDragPointer).Length;

        if (now - _lastDragRenderAtUtc < TimeSpan.FromMilliseconds(DragRenderIntervalMs) && movedDistance < DragRenderMinDistance)
        {
            return false;
        }

        _lastDragRenderAtUtc = now;
        _lastRenderedDragPointer = pointer;
        return true;
    }

    private void DiagramCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == DiagramCanvas)
        {
            CommitInlineEditIfAny();
            SelectNode(null, reRender: true);
        }
    }

    private void NodeCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: DiagramNode node })
        {
            return;
        }

        if (FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            BeginInlineEdit(node.Id);
            e.Handled = true;
            return;
        }

        CommitInlineEditIfAny();
        SelectNode(node.Id, reRender: true);

        _dragNode = node;
        _isDragging = true;
        _dragChanged = false;

        var pointer = GetWorkspacePointerFromMouse(e);
        _dragOffset = new Point(pointer.X - node.X, pointer.Y - node.Y);

        _lastDragRenderAtUtc = DateTime.MinValue;
        _lastRenderedDragPointer = pointer;

        DiagramCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void DiagramCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragNode is null)
        {
            return;
        }

        var pointer = GetWorkspacePointerFromMouse(e);
        var newX = Math.Max(10, pointer.X - _dragOffset.X);
        var newY = Math.Max(10, pointer.Y - _dragOffset.Y);

        if (IsSnapEnabled())
        {
            newX = SnapCoordinate(newX);
            newY = SnapCoordinate(newY);
        }

        if (Math.Abs(newX - _dragNode.X) < 0.1 && Math.Abs(newY - _dragNode.Y) < 0.1)
        {
            return;
        }

        _dragNode.X = newX;
        _dragNode.Y = newY;
        NormalizeNodeToWorkspace(_dragNode);
        _dragChanged = true;

        if (!ShouldRenderDuringDrag(pointer))
        {
            return;
        }

        RenderDiagram();
    }

    private void DiagramCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _dragNode = null;
        _isDragging = false;
        DiagramCanvas.ReleaseMouseCapture();

        RenderDiagram();
        if (_dragChanged)
        {
            CommitState("Node position updated.");
        }
    }

    private void NodeCard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DiagramNode node })
        {
            return;
        }

        CommitInlineEditIfAny();
        SelectNode(node.Id, reRender: false);

        if (_pendingConnectionSourceId is null)
        {
            _pendingConnectionSourceId = node.Id;
            FromNodeComboBox.SelectedItem = node.Id;
            RenderDiagram();
            StatusTextBlock.Text = $"Source selected: {node.Id}. Right-click a target node.";
            return;
        }

        if (_pendingConnectionSourceId.Equals(node.Id, StringComparison.OrdinalIgnoreCase))
        {
            _pendingConnectionSourceId = null;
            RenderDiagram();
            StatusTextBlock.Text = "Connection selection canceled.";
            return;
        }

        var sourceId = _pendingConnectionSourceId;
        _pendingConnectionSourceId = null;
        FromNodeComboBox.SelectedItem = sourceId;
        ToNodeComboBox.SelectedItem = node.Id;
        TryCreateConnection(sourceId, node.Id);
    }

    private void BeginInlineEdit(string nodeId)
    {
        _inlineEditNodeId = nodeId;
        RenderDiagram();
    }

    private void CommitInlineEditIfAny()
    {
        if (_inlineEditNodeId is null)
        {
            return;
        }

        _inlineEditNodeId = null;
        RenderDiagram();
    }

    private void CommitInlineEdit(string? nodeId, string? newTitle, bool canceled)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            _inlineEditNodeId = null;
            RenderDiagram();
            return;
        }

        var node = _diagram.FindNode(nodeId);
        if (node is null)
        {
            _inlineEditNodeId = null;
            RenderDiagram();
            return;
        }

        var oldTitle = node.Title;
        if (!canceled)
        {
            var normalized = string.IsNullOrWhiteSpace(newTitle) ? oldTitle : newTitle.Trim();
            node.Title = normalized;
        }

        _inlineEditNodeId = null;
        UpdateSelectionPanel();
        RenderDiagram();

        if (!canceled && !string.Equals(oldTitle, node.Title, StringComparison.Ordinal))
        {
            CommitState($"{node.Id} text updated.");
        }
    }

    private void CopySelectedNode()
    {
        if (_selectedNodeId is null)
        {
            StatusTextBlock.Text = "Select a node to copy.";
            return;
        }

        var node = _diagram.FindNode(_selectedNodeId);
        if (node is null)
        {
            StatusTextBlock.Text = "Node to copy was not found.";
            return;
        }

        _clipboardNode = new NodeClipboardData(node.Title, node.ShapeType, node.X, node.Y);
        _pasteOffsetStep = 0;
        StatusTextBlock.Text = $"{node.Id} copied to clipboard.";
    }

    private void PasteClipboardNode()
    {
        if (_clipboardNode is null)
        {
            StatusTextBlock.Text = "Clipboard is empty. Copy a node first with Ctrl+C.";
            return;
        }

        _pasteOffsetStep++;
        var offset = 36 + _pasteOffsetStep * 12;

        var x = _clipboardNode.X + offset;
        var y = _clipboardNode.Y + offset;

        if (_selectedNodeId is not null)
        {
            var selected = _diagram.FindNode(_selectedNodeId);
            if (selected is not null)
            {
                x = selected.X + offset;
                y = selected.Y + offset;
            }
        }

        if (IsSnapEnabled())
        {
            x = SnapCoordinate(x);
            y = SnapCoordinate(y);
        }

        var title = _clipboardNode.Title + " Copy";
        var node = _diagram.AddNode(title, Math.Max(10, x), Math.Max(10, y), _clipboardNode.ShapeType);
        NormalizeNodeToWorkspace(node);

        RefreshNodeSelectors();
        SelectNode(node.Id, reRender: false);
        RenderDiagram();
        CommitState($"Node pasted: {node.Id}");
    }

    private void DuplicateSelectedNode()
    {
        CopySelectedNode();
        PasteClipboardNode();
    }

}
