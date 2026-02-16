using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DiagramApp.Models;

namespace DiagramApp.Views;

public partial class MainWindow
{
    private void RenderDiagram()

    {

        var sw = Stopwatch.StartNew();

        DiagramCanvas.Children.Clear();



        var nodeCount = _diagram.Nodes.Count;

        var edgeCount = _diagram.Edges.Count;

        _lightweightRender = _isDragging || nodeCount > 180 || edgeCount > 360;

        var drawArrows = !_lightweightRender || edgeCount < 280;



        foreach (var edge in _diagram.Edges)

        {

            var from = _diagram.FindNode(edge.FromId);

            var to = _diagram.FindNode(edge.ToId);

            if (from is null || to is null)

            {

                continue;

            }



            var fromCenter = GetNodeCenter(from);

            var toCenter = GetNodeCenter(to);



            var start = GetConnectorPoint(from, toCenter);

            var end = GetConnectorPoint(to, fromCenter);



            if (_lightweightRender)

            {

                DiagramCanvas.Children.Add(new Line

                {

                    X1 = start.X,

                    Y1 = start.Y,

                    X2 = end.X,

                    Y2 = end.Y,

                    Stroke = _edgeBrush,

                    StrokeThickness = 1.8,

                    SnapsToDevicePixels = true

                });



                if (drawArrows)

                {

                    DiagramCanvas.Children.Add(BuildArrow(end, end - start));

                }

            }

            else

            {

                var offset = Math.Max(55, Math.Abs(end.X - start.X) * 0.35);

                var c1 = new Point(start.X + offset, start.Y);

                var c2 = new Point(end.X - offset, end.Y);



                DiagramCanvas.Children.Add(new System.Windows.Shapes.Path

                {

                    Stroke = _edgeBrush,

                    StrokeThickness = 2.2,

                    Data = BuildBezier(start, c1, c2, end),

                    SnapsToDevicePixels = true

                });



                if (drawArrows)

                {

                    DiagramCanvas.Children.Add(BuildArrow(end, end - c2));

                }

            }

        }



        var nodes = _diagram.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToList();

        if (_selectedNodeId is not null)

        {

            var selected = nodes.FirstOrDefault(n => n.Id.Equals(_selectedNodeId, StringComparison.OrdinalIgnoreCase));

            if (selected is not null)

            {

                nodes.Remove(selected);

                nodes.Add(selected);

            }

        }



        foreach (var node in nodes)

        {

            var visual = BuildNodeCard(node);

            Canvas.SetLeft(visual, node.X);

            Canvas.SetTop(visual, node.Y);

            DiagramCanvas.Children.Add(visual);

        }



        EmptyStateText.Visibility = nodeCount == 0 ? Visibility.Visible : Visibility.Collapsed;



        sw.Stop();

        _renderCount++;

        _renderMsTotal += sw.Elapsed.TotalMilliseconds;

        var avg = _renderCount == 0 ? 0 : _renderMsTotal / _renderCount;

        RenderPerfTextBlock.Text = $"Render: {sw.Elapsed.TotalMilliseconds:0.0} ms | Avg: {avg:0.0} ms | Mod: {(_lightweightRender ? "Lite" : "Full")}";

    }



    private static PathGeometry BuildBezier(Point start, Point c1, Point c2, Point end)

    {

        var figure = new PathFigure { StartPoint = start };

        figure.Segments.Add(new BezierSegment(c1, c2, end, true));

        return new PathGeometry([figure]);

    }



    private Polygon BuildArrow(Point end, Vector tangent)

    {

        if (tangent.LengthSquared < 0.001)

        {

            tangent = new Vector(1, 0);

        }



        tangent.Normalize();

        var perp = new Vector(-tangent.Y, tangent.X);

        const double size = 9;



        var p1 = end;

        var p2 = end - tangent * size + perp * (size * 0.55);

        var p3 = end - tangent * size - perp * (size * 0.55);



        return new Polygon

        {

            Fill = _edgeBrush,

            Points = new PointCollection([p1, p2, p3])

        };

    }



    private FrameworkElement BuildNodeCard(DiagramNode node)

    {

        var isPendingSource = node.Id.Equals(_pendingConnectionSourceId, StringComparison.OrdinalIgnoreCase);

        var isSelected = node.Id.Equals(_selectedNodeId, StringComparison.OrdinalIgnoreCase);



        Brush border = _nodeBorderBrush;

        var borderThickness = 1.2;



        if (isSelected)

        {

            border = _nodeSelectedBrush;

            borderThickness = 2.0;

        }



        if (isPendingSource)

        {

            border = _nodePendingBrush;

            borderThickness = 2.4;

        }



        var root = new Grid

        {

            Width = NodeWidth,

            Height = NodeHeight,

            Cursor = Cursors.Hand,

            Tag = node,

            SnapsToDevicePixels = true

        };

        ToolTipService.SetShowDuration(root, 60000);
        root.ToolTip = BuildNodeToolTip(node);



        root.Children.Add(BuildNodeShape(node.ShapeType, border, borderThickness));



        if (_inlineEditNodeId is not null && node.Id.Equals(_inlineEditNodeId, StringComparison.OrdinalIgnoreCase))

        {

            root.Children.Add(BuildInlineEditHost(node));

        }

        else

        {

            root.Children.Add(BuildReadOnlyNodeContent(node));

        }



        if (!_lightweightRender)

        {

            root.Effect = new DropShadowEffect

            {

                Color = _nodeShadowBrush.Color,

                BlurRadius = 16,

                ShadowDepth = 1.5,

                Opacity = 0.35

            };

        }



        root.MouseLeftButtonDown += NodeCard_MouseLeftButtonDown;

        root.MouseRightButtonUp += NodeCard_MouseRightButtonUp;

        return root;

    }



    private FrameworkElement BuildNodeToolTip(DiagramNode node)

    {

        var description = string.IsNullOrWhiteSpace(node.Description) ? "Aciklama yok." : node.Description;

        var panel = new StackPanel

        {

            MaxWidth = 360

        };



        panel.Children.Add(new TextBlock

        {

            Text = $"{node.Id} - {node.Title}",

            FontWeight = FontWeights.SemiBold,

            Foreground = Brushes.White

        });



        panel.Children.Add(new TextBlock

        {

            Margin = new Thickness(0, 4, 0, 0),

            Text = $"Tip: {node.ShapeType} | Konum: ({node.X:0}, {node.Y:0})",

            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),

            FontSize = 12

        });



        panel.Children.Add(new TextBlock

        {

            Margin = new Thickness(0, 8, 0, 0),

            Text = description,

            TextWrapping = TextWrapping.Wrap,

            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))

        });



        return new Border

        {

            Padding = new Thickness(10, 8, 10, 8),

            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),

            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),

            BorderThickness = new Thickness(1),

            CornerRadius = new CornerRadius(8),

            Child = panel

        };

    }



    private Shape BuildNodeShape(NodeShapeType shapeType, Brush borderBrush, double borderThickness)

    {

        var fill = new LinearGradientBrush(_nodeCardStartColor, _nodeCardEndColor, new Point(0, 0), new Point(1, 1));



        return shapeType switch

        {

            NodeShapeType.Ellipse => new Ellipse

            {

                Width = NodeWidth,

                Height = NodeHeight,

                Fill = fill,

                Stroke = borderBrush,

                StrokeThickness = borderThickness

            },

            NodeShapeType.DecisionDiamond => new Polygon

            {

                Fill = fill,

                Stroke = borderBrush,

                StrokeThickness = borderThickness,

                Points = new PointCollection

                {

                    new(NodeWidth / 2, 0),

                    new(NodeWidth, NodeHeight / 2),

                    new(NodeWidth / 2, NodeHeight),

                    new(0, NodeHeight / 2)

                }

            },

            _ => new Rectangle

            {

                RadiusX = 14,

                RadiusY = 14,

                Width = NodeWidth,

                Height = NodeHeight,

                Fill = fill,

                Stroke = borderBrush,

                StrokeThickness = borderThickness

            }

        };

    }



    private FrameworkElement BuildReadOnlyNodeContent(DiagramNode node)

    {

        var panel = new StackPanel

        {

            Margin = node.ShapeType == NodeShapeType.DecisionDiamond

                ? new Thickness(22, 18, 22, 14)

                : new Thickness(14, 10, 14, 8),

            VerticalAlignment = VerticalAlignment.Center

        };



        panel.Children.Add(new TextBlock

        {

            Text = node.Id,

            FontSize = 12,

            FontWeight = FontWeights.SemiBold,

            Foreground = _nodeHeaderBrush,

            HorizontalAlignment = HorizontalAlignment.Left

        });



        panel.Children.Add(new TextBlock

        {

            Text = node.Title,

            Margin = new Thickness(0, 6, 0, 0),

            FontSize = 15,

            FontWeight = FontWeights.SemiBold,

            Foreground = _nodeTitleBrush,

            TextAlignment = TextAlignment.Center,

            TextTrimming = TextTrimming.CharacterEllipsis

        });

        if (!string.IsNullOrWhiteSpace(node.Description))

        {

            panel.Children.Add(new TextBlock

            {

                Text = node.Description,

                Margin = new Thickness(0, 4, 0, 0),

                FontSize = 11,

                Foreground = _nodeHeaderBrush,

                TextAlignment = TextAlignment.Center,

                TextWrapping = TextWrapping.NoWrap,

                TextTrimming = TextTrimming.CharacterEllipsis

            });

        }



        return panel;

    }



    private FrameworkElement BuildInlineEditHost(DiagramNode node)

    {

        var editor = new TextBox

        {

            Text = node.Title,

            Margin = new Thickness(16),

            VerticalAlignment = VerticalAlignment.Center,

            HorizontalContentAlignment = HorizontalAlignment.Center,

            FontWeight = FontWeights.SemiBold,

            FontSize = 14,

            Tag = node.Id,

            Background = Brushes.White,

            Foreground = Brushes.Black,

            BorderBrush = Brushes.Transparent,

            BorderThickness = new Thickness(0),

            Padding = new Thickness(8, 5, 8, 5)

        };



        editor.Loaded += InlineEditor_Loaded;

        editor.LostFocus += InlineEditor_LostFocus;

        editor.KeyDown += InlineEditor_KeyDown;

        return editor;

    }



    private void InlineEditor_Loaded(object sender, RoutedEventArgs e)

    {

        if (sender is not TextBox textBox)

        {

            return;

        }



        textBox.Focus();

        textBox.SelectAll();

    }



    private void InlineEditor_LostFocus(object sender, RoutedEventArgs e)

    {

        if (sender is not TextBox textBox)

        {

            return;

        }



        CommitInlineEdit(textBox.Tag as string, textBox.Text, canceled: false);

    }



    private void InlineEditor_KeyDown(object sender, KeyEventArgs e)

    {

        if (sender is not TextBox textBox)

        {

            return;

        }



        if (e.Key == Key.Enter)

        {

            CommitInlineEdit(textBox.Tag as string, textBox.Text, canceled: false);

            e.Handled = true;

            return;

        }



        if (e.Key == Key.Escape)

        {

            CommitInlineEdit(textBox.Tag as string, textBox.Text, canceled: true);

            e.Handled = true;

        }

    }



}
