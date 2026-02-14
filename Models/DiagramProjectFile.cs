using System.Collections.Generic;

namespace DiagramApp.Models;

/// <summary>
/// Dosyaya yazilan diagram proje serilestirme modeli.
/// </summary>
internal sealed class DiagramProjectFile
{
    public string Version { get; set; } = "1.0";
    public string ThemeKey { get; set; } = "Ocean";
    public double CanvasWidth { get; set; } = 2500;
    public double CanvasHeight { get; set; } = 1600;
    public List<DiagramNodeFile> Nodes { get; set; } = [];
    public List<DiagramEdgeFile> Edges { get; set; } = [];
}

internal sealed class DiagramNodeFile
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string ShapeType { get; set; } = nameof(NodeShapeType.Rectangle);
}

internal sealed class DiagramEdgeFile
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
}
