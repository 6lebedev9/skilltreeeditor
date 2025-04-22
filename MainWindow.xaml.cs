using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SkillTreeEditor
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<SkillNode> _nodes = new ObservableCollection<SkillNode>();
        private SkillNode? _selectedNode = null;
        private SkillNode? _connectionSourceNode = null;
        private Point _offset;
        private Point _lastDragPoint = new Point(0, 0);
        private Point _lastCameraDragPoint;
        private bool _isDragging = false;
        private bool _isAddingConnection = false;
        private bool _isRemovingConnection = false;
        private const string SaveFileName = "skilltree.txt";
        private ScaleTransform _canvasScaleTransform = new ScaleTransform();
        private TranslateTransform _canvasTranslateTransform = new TranslateTransform();
        private double _scale = 1.0;
        private Point _cameraPosition = new Point(0, 0);

        public MainWindow()
        {
            InitializeComponent();
            MainCanvas.Background = Brushes.Transparent;
            MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            MainScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            InitializeTransform();
            InitializeCamera();
            InitializeCameraControls();
            NodeTypeComboBox.SelectionChanged += NodeTypeComboBox_SelectionChanged;
            this.KeyDown += Window_KeyDown;
            LoadFromFile();
            DrawNodes();
        }

        private void InitializeTransform()
        {
            var transformGroup = MainCanvas.RenderTransform as TransformGroup;

            _canvasScaleTransform = transformGroup.Children[0] as ScaleTransform;
            _canvasTranslateTransform = transformGroup.Children[1] as TranslateTransform;

            _canvasScaleTransform.ScaleX = _scale;
            _canvasScaleTransform.ScaleY = _scale;
            _canvasTranslateTransform.X = _cameraPosition.X;
            _canvasTranslateTransform.Y = _cameraPosition.Y;
        }

        private void UpdateCanvasSize()
        {
            double minX = _nodes.Min(n => n.X) - 700;
            double maxX = _nodes.Max(n => n.X) + 700;
            double minY = _nodes.Min(n => n.Y) - 700;
            double maxY = _nodes.Max(n => n.Y) + 700;

            MainCanvas.Width = Math.Max(maxX - minX, 15000);
            MainCanvas.Height = Math.Max(maxY - minY, 15000);

            _cameraPosition = new Point(-minX, -minY);
            UpdateCameraTransform();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_canvasScaleTransform == null) return; 

            _scale = e.NewValue;
            _canvasScaleTransform.ScaleX = _scale;
            _canvasScaleTransform.ScaleY = _scale;

            if (_canvasTranslateTransform != null)
            {
                UpdateCameraTransform();
            }
        }

        private void CameraXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _cameraPosition.X = e.NewValue;
            UpdateCameraTransform();
            CameraXTextBox.Text = e.NewValue.ToString("0");
        }

        private void CameraYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _cameraPosition.Y = e.NewValue;
            UpdateCameraTransform();
            CameraYTextBox.Text = e.NewValue.ToString("0");
        }

        private void InitializeCameraControls()
        {
            CameraXSlider.Value = _cameraPosition.X;
            CameraYSlider.Value = _cameraPosition.Y;
            ScaleSlider.Value = _scale;

            CameraXSlider.ValueChanged += CameraXSlider_ValueChanged;
            CameraYSlider.ValueChanged += CameraYSlider_ValueChanged;
            ScaleSlider.ValueChanged += ScaleSlider_ValueChanged;

            CameraXTextBox.Text = _cameraPosition.X.ToString("0");
            CameraYTextBox.Text = _cameraPosition.Y.ToString("0");
        }

        private void UpdateCameraTransform()
        {
            _canvasTranslateTransform.X = _cameraPosition.X * _scale;
            _canvasTranslateTransform.Y = _cameraPosition.Y * _scale;
        }

        private void InitializeCamera()
        {
            _canvasTranslateTransform.X = _cameraPosition.X * _scale;
            _canvasTranslateTransform.Y = _cameraPosition.Y * _scale;
            MainCanvas.MouseRightButtonDown += MainCanvas_MouseRightButtonDown;
            MainCanvas.MouseRightButtonUp += MainCanvas_MouseRightButtonUp;
            MainCanvas.MouseMove += MainCanvas_MouseMove;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _selectedNode = null;
                ShowNodeInPanel(null);
                DrawNodes();
            }
        }

        private void NodeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedNode != null && NodeTypeComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedNode.NodeType = item.Tag.ToString() ?? "Default";
                DrawNodes();
            }
        }

        private void UpdateCameraPositionText()
        {
            CameraXTextBox.Text = _cameraPosition.X.ToString();
            CameraYTextBox.Text = _cameraPosition.Y.ToString();
        }

        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(SaveFileName))
                {
                    LoadSampleNodes();
                    return;
                }

                var lines = File.ReadAllLines(SaveFileName);
                _nodes.Clear();

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;
                    var node = new SkillNode(parts[0], double.Parse(parts[1]), double.Parse(parts[2]));
                    if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
                    {
                        foreach (var conn in parts[3].Split(','))
                        {
                            if (!string.IsNullOrEmpty(conn))
                                node.Connections.Add(conn);
                        }
                    }
                    if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4]))
                        node.GroupId = int.Parse(parts[4]);
                    if (parts.Length > 5) node.NodeType = parts[5];
                    
                    _nodes.Add(node);
                }
                UpdateCanvasSize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
                LoadSampleNodes();
            }
        }

        private void LoadSampleNodes()
        {
            _nodes.Add(new SkillNode("w0", 100, 100));
            _nodes.Add(new SkillNode("m0", 300, 200));
            _nodes.Add(new SkillNode("r0", 200, 400));
            AddConnection("w0", "m0");
            AddConnection("m0", "r0");
        }

        private void AddConnection(string fromId, string toId)
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == fromId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == toId);

            if (fromNode != null && toNode != null)
            {
                if (!fromNode.Connections.Contains(toId))
                    fromNode.Connections.Add(toId);

                if (!toNode.Connections.Contains(fromId))
                    toNode.Connections.Add(fromId);
            }
        }

        private void DrawNodes()
        {
            MainCanvas.Children.Clear();

            // 1. Сначала рисуем все связи
            foreach (var node in _nodes)
            {
                foreach (var conn in node.Connections)
                {
                    var target = _nodes.FirstOrDefault(n => n.Id == conn);
                    if (target != null)
                    {
                        var line = new Line
                        {
                            X1 = node.X + 25,
                            Y1 = node.Y + 25,
                            X2 = target.X + 25,
                            Y2 = target.Y + 25,
                            Stroke = Brushes.Black,
                            StrokeThickness = 2,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round
                        };
                        Panel.SetZIndex(line, -1); // Линии под нодами
                        MainCanvas.Children.Add(line);
                    }
                }
            }

            // 2. Затем рисуем ноды
            foreach (var node in _nodes)
            {
                // Создаем фигуру ноды
                Shape shape;
                switch (node.NodeType)
                {
                    case "Mastery":
                        shape = new Polygon
                        {
                            Points = new PointCollection { new Point(25, 0), new Point(50, 50), new Point(0, 50) },
                            Fill = Brushes.Gold,
                            Stroke = Brushes.Black,
                            StrokeThickness = 2
                        };
                        break;
                    case "Stat":
                        shape = new Polygon
                        {
                            Points = new PointCollection { new Point(25, 0), new Point(50, 25), new Point(25, 50), new Point(0, 25) },
                            Fill = Brushes.LightBlue,
                            Stroke = Brushes.Black,
                            StrokeThickness = 2
                        };
                        break;
                    default: // Default
                        shape = new Ellipse
                        {
                            Width = 50,
                            Height = 50,
                            Fill = node == _selectedNode ? Brushes.LightGreen : Brushes.SkyBlue,
                            Stroke = Brushes.Black,
                            StrokeThickness = 2
                        };
                        break;
                }

                // Настройка фигуры
                shape.Tag = node;
                Canvas.SetLeft(shape, node.X);
                Canvas.SetTop(shape, node.Y);
                shape.MouseLeftButtonDown += Node_MouseLeftButtonDown;
                shape.MouseMove += Node_MouseMove;
                shape.MouseLeftButtonUp += Node_MouseLeftButtonUp;
                MainCanvas.Children.Add(shape);

                // Создаем текст ноды
                var label = new TextBlock
                {
                    Text = node.GroupId.HasValue ? $"{node.Id} ({node.GroupId})" : node.Id,
                    Width = 50,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = node.NodeType == "Mastery" ? FontWeights.Bold : FontWeights.Normal
                };
                Canvas.SetLeft(label, node.X);
                Canvas.SetTop(label, node.Y + (node.NodeType == "Mastery" ? 55 : 15));
                MainCanvas.Children.Add(label);
            }
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed) return;

            if (sender is Shape shape && shape.Tag is SkillNode node) // Изменено с Ellipse на Shape
            {
                _selectedNode = node;
                _offset = e.GetPosition(MainCanvas);
                _offset.X -= node.X;
                _offset.Y -= node.Y;
                shape.CaptureMouse();
                ShowNodeInPanel(node);
                DrawNodes();
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastCameraDragPoint = e.GetPosition(MainScrollViewer);
            _isDragging = true;
            MainCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_selectedNode != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(MainCanvas);
                _selectedNode.X = position.X - _offset.X;
                _selectedNode.Y = position.Y - _offset.Y;

                NodeXTextBox.Text = _selectedNode.X.ToString();
                NodeYTextBox.Text = _selectedNode.Y.ToString();

                DrawNodes();
            }
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse ellipse)
            {
                ellipse.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            _scale = Math.Min(3, Math.Max(0.1, _scale * zoomFactor));

            _canvasScaleTransform.ScaleX = _scale;
            _canvasScaleTransform.ScaleY = _scale;
            ScaleSlider.Value = _scale;

            e.Handled = true;
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastDragPoint = e.GetPosition(this);
            MainCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentPos = e.GetPosition(MainScrollViewer);
                double deltaX = currentPos.X - _lastCameraDragPoint.X;
                double deltaY = currentPos.Y - _lastCameraDragPoint.Y;

                _cameraPosition.X += deltaX / _scale;
                _cameraPosition.Y += deltaY / _scale;

                UpdateCameraTransform();
                _lastCameraDragPoint = currentPos;
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MainCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_lastDragPoint != new Point(0,0))
            {
                MainCanvas.ReleaseMouseCapture();
                _lastDragPoint = new Point(0,0);
                e.Handled = true;
            }
        }

        private void ShowNodeInPanel(SkillNode? node)
        {
            if (node == null)
            {
                NodeIdTextBox.Text = string.Empty;
                NodeXTextBox.Text = string.Empty;
                NodeYTextBox.Text = string.Empty;
                NodeConnectionsListBox.ItemsSource = null;
                GroupIdTextBox.Text = string.Empty;
                NodeTypeComboBox.SelectedIndex = 0;
                return;
            }

            NodeIdTextBox.Text = node.Id;
            NodeXTextBox.Text = node.X.ToString();
            NodeYTextBox.Text = node.Y.ToString();
            NodeConnectionsListBox.ItemsSource = node.Connections;
            GroupIdTextBox.Text = node.GroupId?.ToString() ?? "";
            NodeTypeComboBox.SelectedIndex = node.NodeType switch
            {
                "Mastery" => 1,
                "Stat" => 2,
                _ => 0
            };
        }

        private void SaveNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                _selectedNode.Id = NodeIdTextBox.Text;
                if (double.TryParse(NodeXTextBox.Text, out double x)) _selectedNode.X = x;
                if (double.TryParse(NodeYTextBox.Text, out double y)) _selectedNode.Y = y;

                SaveToFile();
                DrawNodes();
                UpdateCanvasSize();
            }
        }

        private void SaveToFile()
        {
            try
            {
                using (var writer = new StreamWriter(SaveFileName))
                {
                    foreach (var node in _nodes)
                    {
                        string connections = string.Join(",", node.Connections);
                        writer.WriteLine($"{node.Id}|{node.X}|{node.Y}|{connections}|{node.GroupId}|{node.NodeType}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void AddConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                _connectionSourceNode = _selectedNode;
                _isAddingConnection = true;
                _isRemovingConnection = false;
            }
        }

        private void RemoveConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                _connectionSourceNode = _selectedNode;
                _isRemovingConnection = true;
                _isAddingConnection = false;
            }
        }

        private void RemoveConnection(string fromId, string toId)
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == fromId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == toId);

            if (fromNode != null)
            {
                fromNode.Connections.Remove(toId);
            }

            if (toNode != null)
            {
                toNode.Connections.Remove(fromId);
            }
        }

        private void ApplyGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                if (int.TryParse(GroupIdTextBox.Text, out int groupId))
                {
                    _selectedNode.GroupId = groupId;
                }
                else
                {
                    _selectedNode.GroupId = null;
                }
                DrawNodes();
            }
        }
    }

    public class SkillNode
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public ObservableCollection<string> Connections { get; set; }
        public int? GroupId { get; set; }
        public string NodeType { get; set; }

        public SkillNode()
        {
            Id = string.Empty;
            Connections = new ObservableCollection<string>();
            NodeType = "Default";
        }

        public SkillNode(string id, double x, double y) : this()
        {
            Id = id;
            X = x;
            Y = y;
        }
    }
}