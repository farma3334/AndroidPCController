using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AndroidPCController.App.ViewModels;

namespace AndroidPCController.App.Pages;

public partial class AutomationPage : UserControl
{
    private FlowNodeViewModel? _dragNode;
    private Point _dragStart;
    private double _dragOriginX;
    private double _dragOriginY;
    private bool _isDragging;

    public AutomationPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RedrawWires();
        SizeChanged += (_, _) => RedrawWires();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AutomationViewModel oldVm)
        {
            oldVm.Nodes.CollectionChanged -= OnNodesChanged;
        }

        if (DataContext is AutomationViewModel vm)
        {
            vm.Nodes.CollectionChanged += OnNodesChanged;
            foreach (var node in vm.Nodes)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                node.PropertyChanged += OnNodePropertyChanged;
            }
            RedrawWires();
        }
    }

    private void OnNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (FlowNodeViewModel node in e.OldItems)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (FlowNodeViewModel node in e.NewItems)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }

        RedrawWires();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowNodeViewModel.X) || e.PropertyName == nameof(FlowNodeViewModel.Y))
        {
            RedrawWires();
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button) return;
        if (DataContext is not AutomationViewModel { IsRunning: false } vm) return;

        if (sender is FrameworkElement element && element.DataContext is FlowNodeViewModel node)
        {
            _dragNode = node;
            _dragStart = e.GetPosition(NodeHost);
            _dragOriginX = node.X;
            _dragOriginY = node.Y;
            _isDragging = false;
            NodeHost.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Node_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragNode is null || !NodeHost.IsMouseCaptured) return;

        var current = e.GetPosition(NodeHost);
        var delta = current - _dragStart;
        if (!_isDragging && Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3) return;
        _isDragging = true;

        var newX = Math.Max(0, _dragOriginX + delta.X);
        var newY = Math.Max(0, _dragOriginY + delta.Y);

        if (NodeHost.ActualWidth > FlowNodeViewModel.NodeWidth)
        {
            newX = Math.Min(newX, NodeHost.ActualWidth - FlowNodeViewModel.NodeWidth - 8);
        }
        if (NodeHost.ActualHeight > 80)
        {
            newY = Math.Min(newY, NodeHost.ActualHeight - 80);
        }

        _dragNode.X = newX;
        _dragNode.Y = newY;
        RedrawWires();
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragNode is null) return;
        NodeHost.ReleaseMouseCapture();
        _dragNode = null;
        _isDragging = false;
    }

    private void Node_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_dragNode is null) return;
        NodeHost.ReleaseMouseCapture();
        _dragNode = null;
        _isDragging = false;
    }

    private void RedrawWires()
    {
        WireCanvas.Children.Clear();
        if (DataContext is not AutomationViewModel vm || vm.Nodes.Count < 2) return;

        const double portY = FlowNodeViewModel.HeaderHeight / 2 + 8;

        for (var i = 0; i < vm.Nodes.Count - 1; i++)
        {
            var from = vm.Nodes[i];
            var to = vm.Nodes[i + 1];

            var x1 = from.X + FlowNodeViewModel.NodeWidth - 4;
            var y1 = from.Y + portY;
            var x2 = to.X + 4;
            var y2 = to.Y + portY;

            var midX = (x1 + x2) / 2;

            var path = new Path
            {
                Stroke = new LinearGradientBrush(
                    Color.FromRgb(0x8B, 0x5C, 0xF6),
                    Color.FromRgb(0x34, 0xD3, 0x99),
                    0),
                StrokeThickness = 2,
                Data = new PathGeometry(new[]
                {
                    new PathFigure(new Point(x1, y1),
                    [
                        new BezierSegment(new Point(midX, y1), new Point(midX, y2), new Point(x2, y2), true)
                    ], false)
                })
            };

            WireCanvas.Children.Add(path);
        }
    }
}