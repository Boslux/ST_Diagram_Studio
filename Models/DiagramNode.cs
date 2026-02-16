using System;

namespace DiagramApp.Models;

/// <summary>
/// Diagram uzerinde gosterilen tek bir dugumu temsil eder.
/// </summary>
internal sealed class DiagramNode
{
    public DiagramNode(string id, string title, double x, double y, NodeShapeType shapeType = NodeShapeType.Rectangle, string? description = null)
    {
        Id = id;
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        X = x;
        Y = y;
        ShapeType = shapeType;
    }

    /// <summary>
    /// Dugum kimligi (N1, N2...).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Dugum basligi.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Dugum aciklamasi.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Canvas X koordinati.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Canvas Y koordinati.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Dugum sekli.
    /// </summary>
    public NodeShapeType ShapeType { get; set; }
}
