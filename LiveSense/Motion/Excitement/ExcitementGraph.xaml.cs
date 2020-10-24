using LiveSense.Common;
using PropertyChanged;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LiveSense.Motion.Excitement
{
    /// <summary>
    /// Interaction logic for ExcitementGraph.xaml
    /// </summary>
    [SuppressPropertyChangedWarnings]
    public partial class ExcitementGraph : UserControl, INotifyPropertyChanged
    {
        public BindableCollection<UIElement> GraphLines { get; set; }
        public BindableCollection<LegendItem> LegendItems { get; set; }

        public BindableCollection<IExcitementGraphItem> ItemsSource
        {
            get { return (BindableCollection<IExcitementGraphItem>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(BindableCollection<IExcitementGraphItem>), typeof(ExcitementGraph),
                new FrameworkPropertyMetadata(null, 
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, 
                    new PropertyChangedCallback(OnItemsSourceChanged)));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ExcitementGraph graph))
                return;

            if (e.OldValue is BindableCollection<IExcitementGraphItem> oldItems)
                oldItems.CollectionChanged -= graph.OnItemsSourceCollectionChanged;
            if (e.NewValue is BindableCollection<IExcitementGraphItem> newItems)
                newItems.CollectionChanged += graph.OnItemsSourceCollectionChanged;

            graph.Refresh();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ExcitementGraph()
        {
            InitializeComponent();
            GraphLines = new BindableCollection<UIElement>();
            LegendItems = new BindableCollection<LegendItem>();
        }

        private void Refresh()
        {
            if (ItemsSource == null)
                return;

            var first = ItemsSource.FirstOrDefault();
            var last = ItemsSource.LastOrDefault();
            if (first == null || last == null || first == last)
                return;

            var properties = first.GetType().GetProperties().Where(p => p.PropertyType == typeof(ExcitementGraphProperty)).ToList();
            if(GraphLines.Count == 0)
                InitializeElements(properties);

            var timeRange = (float)(last.Timestamp - first.Timestamp).TotalSeconds;
            foreach(var line in GraphLines.Cast<Polyline>())
                line.Points.Clear();

            foreach (var item in ItemsSource)
            {
                var index = 0;
                foreach (var property in properties)
                {
                    var line = GraphLines[index++] as Polyline;

                    var graphProperty = (ExcitementGraphProperty)property.GetValue(item);
                    var x = MathUtils.Map((float)(item.Timestamp - first.Timestamp).TotalSeconds, 0, timeRange, 0, (float)Canvas.ActualWidth);
                    var y = MathUtils.Map(graphProperty.Value, graphProperty.Minimum, graphProperty.Maximum, (float)Canvas.ActualHeight, 0);

                    line.Points.Add(new Point(x, y));
                    line.InvalidateVisual();
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GraphLines)));
            InvalidateVisual();
        }

        private void InitializeElements(List<PropertyInfo> properties)
        {
            GraphLines.Clear();
            LegendItems.Clear();

            var colors = new Color[]
            {
                    Color.FromRgb(84,110,122),
                    Color.FromRgb(229,57,53),
                    Color.FromRgb(94,53,177),
                    Color.FromRgb(30,136,229),
                    Color.FromRgb(0,172,193),
                    Color.FromRgb(67,160,71),
                    Color.FromRgb(192,202,51),
                    Color.FromRgb(255,179,0),
                    Color.FromRgb(244,81,30),
                    Color.FromRgb(109,76,65)
            };

            var index = 0;
            foreach(var property in properties)
            {
                var color = colors[index++ % colors.Length];
                LegendItems.Add(new LegendItem()
                { 
                    Value = property.Name, 
                    Color = new SolidColorBrush(color)
                });

                GraphLines.Add(new Polyline()
                {
                    StrokeThickness = 2,
                    Stroke = new SolidColorBrush(color)
                });
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Refresh();
        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Refresh();
    }

    public interface IExcitementGraphItem
    {
        DateTime Timestamp { get; }
    }

    public class ExcitementGraphProperty
    {
        public float Value { get; set; }
        public float Minimum { get; set; }
        public float Maximum { get; set; }

        public static implicit operator ExcitementGraphProperty((float Value, float Minimum, float Maximum) tuple)
            => new ExcitementGraphProperty() { Value = tuple.Value, Minimum = tuple.Minimum, Maximum = tuple.Maximum };
    }

    public class LegendItem : PropertyChangedBase
    {
        public string Value { get; set; }
        public Brush Color { get; set; }
    }
}
