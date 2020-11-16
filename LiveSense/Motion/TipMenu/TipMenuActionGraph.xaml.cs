using PropertyChanged;
using Stylet;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LiveSense.Motion.TipMenu
{
    /// <summary>
    /// Interaction logic for TipMenuActionGraph.xaml
    /// </summary>
    ///
    public class PointCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(PointCollection) && value is BindableCollection<Point> collection)
                return new PointCollection(collection);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(BindableCollection<Point>) && value is PointCollection collection)
                return new BindableCollection<Point>(collection);

            return null;
        }
    }

    [SuppressPropertyChangedWarnings]
    public partial class TipMenuActionGraph : UserControl, INotifyPropertyChanged
    {
        public BindableCollection<Point> Points { get; set; }
        public float ActualDuration { get; set; }

        [DoNotNotify]
        public Brush Fill
        {
            get { return (Brush)GetValue(FillProperty); }
            set { SetValue(FillProperty, value); }
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(Brush), typeof(TipMenuActionGraph), new PropertyMetadata(null));

        [DoNotNotify]
        public Brush Stroke
        {
            get { return (Brush)GetValue(StrokeProperty); }
            set { SetValue(StrokeProperty, value); }
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(TipMenuActionGraph), new PropertyMetadata(null));

        [DoNotNotify]
        public int StrokeThickness
        {
            get { return (int)GetValue(StrokeThicknessProperty); }
            set { SetValue(StrokeThicknessProperty, value); }
        }

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register("StrokeThickness", typeof(int), typeof(TipMenuActionGraph), new PropertyMetadata(null));

        public TipMenuAction Action
        {
            get { return (TipMenuAction)GetValue(ActionProperty); }
            set { SetValue(ActionProperty, value); }
        }

        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register("Action", typeof(TipMenuAction), typeof(TipMenuActionGraph),
                new FrameworkPropertyMetadata(default(TipMenuAction),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    new PropertyChangedCallback(OnActionChanged)));

        public int Duration
        {
            get { return (int)GetValue(DurationProperty); }
            set { SetValue(DurationProperty, value); }
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register("Duration", typeof(int), typeof(TipMenuActionGraph),
                new FrameworkPropertyMetadata(default(int),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    new PropertyChangedCallback(OnDurationChanged)));

        public event PropertyChangedEventHandler PropertyChanged;

        private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TipMenuActionGraph graph)
                return;

            if (e.OldValue is TipMenuAction oldAction)
                oldAction.PropertyChanged -= graph.OnActionPropertyChanged;
            if (e.NewValue is TipMenuAction newAction)
                newAction.PropertyChanged += graph.OnActionPropertyChanged;

            graph.Refresh();
        }

        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var graph = (d as TipMenuActionGraph);
            graph?.Refresh();
        }

        public TipMenuActionGraph()
        {
            InitializeComponent();

            Points = new BindableCollection<Point>();
        }

        private void Refresh()
        {
            Points.Clear();

            if (Action != null)
            {
                const int pointCount = 1000;
                const int repeatLimit = 10;

                var limitedDuration = Math.Min(Math.Ceiling((float)Duration / Action.Period), repeatLimit) * Action.Period;
                ActualDuration = (float)Math.Min(Duration, limitedDuration);
                for (var i = 0; i < pointCount; i++)
                {
                    var time = i / (pointCount - 1.0f) * ActualDuration;

                    var actionValue = Action.NormalizeAndCalculate(time);
                    if (!float.IsFinite(actionValue))
                        continue;

                    var value = 1 - actionValue;
                    Points.Add(new Point(time * ActualWidth / ActualDuration, value * ActualHeight));
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Points)));
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Refresh();
        private void OnActionPropertyChanged(object sender, PropertyChangedEventArgs e) => Refresh();
    }
}
