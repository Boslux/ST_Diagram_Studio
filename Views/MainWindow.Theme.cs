using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace DiagramApp.Views;

public partial class MainWindow
{
    private void ApplySelectedTheme()
    {
        var key = GetSelectedThemeKey();
        if (!ThemePalettes.TryGetValue(key, out var palette))
        {
            palette = ThemePalettes["Ocean"];
        }

        var panelForeground = new SolidColorBrush(palette.InputForeground);
        var mutedForeground = new SolidColorBrush(palette.Edge);
        var darkActionForeground = new SolidColorBrush(Color.FromRgb(11, 18, 32));

        RootGradientStartStop.Color = palette.GradientStart;
        RootGradientEndStop.Color = palette.GradientEnd;

        SidePanelBorder.Background = new SolidColorBrush(palette.SidePanel);
        TopToolbarBorder.Background = new SolidColorBrush(palette.Toolbar);
        CanvasHostBorder.Background = new SolidColorBrush(palette.CanvasPanel);
        CanvasHostBorder.BorderBrush = new SolidColorBrush(palette.CanvasBorder);
        BottomStatusBar.Background = new SolidColorBrush(palette.Toolbar);
        TopMainMenu.Foreground = panelForeground;
        ProjectPathTextBlock.Foreground = mutedForeground;
        SelectedNodeTextBlock.Foreground = mutedForeground;
        ZoomTextBlock.Foreground = panelForeground;
        StatusTextBlock.Foreground = panelForeground;
        AutosaveStatusTextBlock.Foreground = panelForeground;
        RenderPerfTextBlock.Foreground = panelForeground;

        NodeTitleTextBox.Background = new SolidColorBrush(palette.InputBackground);
        NodeTitleTextBox.Foreground = new SolidColorBrush(palette.InputForeground);
        NodeDescriptionTextBox.Background = new SolidColorBrush(palette.InputBackground);
        NodeDescriptionTextBox.Foreground = new SolidColorBrush(palette.InputForeground);
        RenameNodeTextBox.Background = new SolidColorBrush(palette.InputBackground);
        RenameNodeTextBox.Foreground = new SolidColorBrush(palette.InputForeground);
        RenameNodeDescriptionTextBox.Background = new SolidColorBrush(palette.InputBackground);
        RenameNodeDescriptionTextBox.Foreground = new SolidColorBrush(palette.InputForeground);

        FromNodeComboBox.Background = new SolidColorBrush(palette.SelectorBackground);
        FromNodeComboBox.Foreground = panelForeground;
        ToNodeComboBox.Background = new SolidColorBrush(palette.SelectorBackground);
        ToNodeComboBox.Foreground = panelForeground;
        ThemeComboBox.Background = new SolidColorBrush(palette.SelectorBackground);
        ThemeComboBox.Foreground = panelForeground;
        NodeShapeComboBox.Background = new SolidColorBrush(palette.SelectorBackground);
        NodeShapeComboBox.Foreground = panelForeground;

        AddNodeButton.Background = new SolidColorBrush(palette.Action);
        AddNodeButton.Foreground = darkActionForeground;
        ConnectButton.Background = new SolidColorBrush(palette.Action);
        ConnectButton.Foreground = darkActionForeground;
        SaveButton.Background = new SolidColorBrush(palette.Action);
        SaveButton.Foreground = panelForeground;
        SaveAsButton.Background = new SolidColorBrush(palette.Action);
        SaveAsButton.Foreground = panelForeground;
        RenameNodeButton.Background = new SolidColorBrush(palette.Action);
        RenameNodeButton.Foreground = darkActionForeground;
        ExportPngButton.Background = new SolidColorBrush(palette.Action);
        ExportPngButton.Foreground = panelForeground;

        AutoLayoutButton.Background = new SolidColorBrush(palette.Utility);
        AutoLayoutButton.Foreground = panelForeground;
        NewProjectButton.Background = new SolidColorBrush(palette.Utility);
        NewProjectButton.Foreground = panelForeground;
        OpenProjectButton.Background = new SolidColorBrush(palette.Utility);
        OpenProjectButton.Foreground = panelForeground;
        UndoButton.Background = new SolidColorBrush(palette.Utility);
        UndoButton.Foreground = panelForeground;
        RedoButton.Background = new SolidColorBrush(palette.Utility);
        RedoButton.Foreground = panelForeground;
        FitViewButton.Background = new SolidColorBrush(palette.Utility);
        FitViewButton.Foreground = panelForeground;
        ZoomOutButton.Background = new SolidColorBrush(palette.Utility);
        ZoomOutButton.Foreground = panelForeground;
        ZoomInButton.Background = new SolidColorBrush(palette.Utility);
        ZoomInButton.Foreground = panelForeground;

        ClearButton.Background = new SolidColorBrush(palette.Danger);
        ClearButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 226, 226));
        DeleteSelectedNodeButton.Background = new SolidColorBrush(palette.Danger);
        DeleteSelectedNodeButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 226, 226));
        RemoveEdgeButton.Background = new SolidColorBrush(palette.Danger);
        RemoveEdgeButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 226, 226));

        _nodeCardStartColor = palette.NodeCardStart;
        _nodeCardEndColor = palette.NodeCardEnd;
        _edgeBrush.Color = palette.Edge;
        _nodeBorderBrush.Color = palette.NodeBorder;
        _nodeSelectedBrush.Color = palette.NodeSelected;
        _nodePendingBrush.Color = palette.NodePending;
        _nodeHeaderBrush.Color = palette.Edge;
        _nodeTitleBrush.Color = palette.InputForeground;
    }

    private string GetSelectedThemeKey()
    {
        return (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Ocean";
    }

    private void SetThemeSelection(string themeKey)
    {
        _isProgrammaticThemeChange = true;
        var selected = ThemeComboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, themeKey, StringComparison.OrdinalIgnoreCase));

        ThemeComboBox.SelectedItem = selected ?? ThemeComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        UpdateThemeMenuSelection((ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string);
        ApplySelectedTheme();
        _isProgrammaticThemeChange = false;
    }

    private void UpdateThemeMenuSelection(string? selectedThemeKey)
    {
        var key = selectedThemeKey ?? "Ocean";
        ThemeOceanMenuItem.IsChecked = string.Equals(key, "Ocean", StringComparison.OrdinalIgnoreCase);
        ThemeForestMenuItem.IsChecked = string.Equals(key, "Forest", StringComparison.OrdinalIgnoreCase);
        ThemeSunsetMenuItem.IsChecked = string.Equals(key, "Sunset", StringComparison.OrdinalIgnoreCase);
    }
}
