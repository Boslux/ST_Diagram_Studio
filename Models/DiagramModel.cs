using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramApp.Models;

/// <summary>
/// Dugumler ve baglantilar icin in-memory model katmani.
/// </summary>
internal sealed class DiagramModel
{
    private readonly Dictionary<string, DiagramNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DiagramEdge> _edges = [];
    private int _nextId = 1;

    public IReadOnlyCollection<DiagramNode> Nodes => _nodes.Values;
    public IReadOnlyList<DiagramEdge> Edges => _edges;

    /// <summary>
    /// Yeni bir dugum ekler ve otomatik id atar.
    /// </summary>
    public DiagramNode AddNode(string title, double x, double y, NodeShapeType shapeType = NodeShapeType.Rectangle, string? description = null)
    {
        var id = CreateId();
        var node = new DiagramNode(id, title, x, y, shapeType, description);
        _nodes[id] = node;
        return node;
    }

    /// <summary>
    /// Iki dugum arasinda yonlu baglanti olusturur.
    /// </summary>
    public bool Connect(string fromId, string toId)
    {
        if (fromId.Equals(toId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_nodes.ContainsKey(fromId) || !_nodes.ContainsKey(toId))
        {
            return false;
        }

        if (HasEdge(fromId, toId))
        {
            return false;
        }

        _edges.Add(new DiagramEdge(fromId, toId));
        return true;
    }

    /// <summary>
    /// Belirtilen baglantinin var olup olmadigini kontrol eder.
    /// </summary>
    public bool HasEdge(string fromId, string toId)
    {
        return _edges.Any(e => e.FromId.Equals(fromId, StringComparison.OrdinalIgnoreCase) &&
                               e.ToId.Equals(toId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Dugumu ve dugume bagli tum atamalari/baglantilari siler.
    /// </summary>
    public bool RemoveNode(string nodeId)
    {
        if (!_nodes.Remove(nodeId))
        {
            return false;
        }

        _edges.RemoveAll(e => e.FromId.Equals(nodeId, StringComparison.OrdinalIgnoreCase) ||
                              e.ToId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    /// <summary>
    /// Belirli bir yonlu baglantiyi siler.
    /// </summary>
    public bool RemoveEdge(string fromId, string toId)
    {
        var index = _edges.FindIndex(e => e.FromId.Equals(fromId, StringComparison.OrdinalIgnoreCase) &&
                                          e.ToId.Equals(toId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        _edges.RemoveAt(index);
        return true;
    }

    public DiagramNode? FindNode(string id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    /// <summary>
    /// Tum diagram durumunu sifirlar.
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _edges.Clear();
        _nextId = 1;
    }

    /// <summary>
    /// Uygulama modelini dosyaya yazilabilir proje seklinde dondurur.
    /// </summary>
    public DiagramProjectFile ExportProject(string themeKey)
    {
        return new DiagramProjectFile
        {
            ThemeKey = themeKey,
            Nodes = _nodes.Values
                .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
                .Select(n => new DiagramNodeFile
                {
                    Id = n.Id,
                    Title = n.Title,
                    Description = n.Description,
                    X = n.X,
                    Y = n.Y,
                    ShapeType = n.ShapeType.ToString()
                })
                .ToList(),
            Edges = _edges
                .Select(e => new DiagramEdgeFile
                {
                    FromId = e.FromId,
                    ToId = e.ToId
                })
                .ToList()
        };
    }

    /// <summary>
    /// Dosyadan okunan proje verisini modele uygular.
    /// </summary>
    public void ImportProject(DiagramProjectFile project)
    {
        _nodes.Clear();
        _edges.Clear();

        foreach (var node in project.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                continue;
            }

            if (_nodes.ContainsKey(node.Id))
            {
                continue;
            }

            _nodes[node.Id] = new DiagramNode(
                node.Id,
                node.Title ?? "New Node",
                node.X,
                node.Y,
                ParseShapeType(node.ShapeType),
                node.Description);
        }

        foreach (var edge in project.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.FromId) || string.IsNullOrWhiteSpace(edge.ToId))
            {
                continue;
            }

            if (edge.FromId.Equals(edge.ToId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_nodes.ContainsKey(edge.FromId) || !_nodes.ContainsKey(edge.ToId))
            {
                continue;
            }

            if (HasEdge(edge.FromId, edge.ToId))
            {
                continue;
            }

            _edges.Add(new DiagramEdge(edge.FromId, edge.ToId));
        }

        _nextId = CalculateNextId(_nodes.Keys);
    }

    private static NodeShapeType ParseShapeType(string? shapeType)
    {
        if (string.IsNullOrWhiteSpace(shapeType))
        {
            return NodeShapeType.Rectangle;
        }

        return Enum.TryParse<NodeShapeType>(shapeType, ignoreCase: true, out var value)
            ? value
            : NodeShapeType.Rectangle;
    }

    private string CreateId()
    {
        while (true)
        {
            var id = $"N{_nextId++}";
            if (!_nodes.ContainsKey(id))
            {
                return id;
            }
        }
    }

    private static int CalculateNextId(IEnumerable<string> nodeIds)
    {
        var max = 0;
        foreach (var id in nodeIds)
        {
            if (id.Length <= 1 || !id.StartsWith("N", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(id[1..], out var number))
            {
                max = Math.Max(max, number);
            }
        }

        return max + 1;
    }
}

