using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiveSense.Common.Behaviours;

public static class ListViewBehavior
{
    public static readonly DependencyProperty SelectRowOnClickProperty =
        DependencyProperty.RegisterAttached("SelectRowOnClick",
            typeof(bool), typeof(ListViewBehavior),
                new PropertyMetadata(false, OnSelectRowOnClickChanged));

    public static bool GetSelectRowOnClick(FrameworkElement element)
        => (bool)element.GetValue(SelectRowOnClickProperty);

    public static void SetSelectRowOnClick(FrameworkElement element, bool value)
        => element.SetValue(SelectRowOnClickProperty, value);

    private static void OnSelectRowOnClickChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        if (depObj is not FrameworkElement element)
            return;

        static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var dependencyObject = (DependencyObject)e.OriginalSource;
            while (dependencyObject != null && dependencyObject is not ListViewItem)
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

            if (dependencyObject is not ListViewItem item)
                return;

            while (dependencyObject != null && dependencyObject is not ListView)
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

            if (dependencyObject is not ListView view)
                return;

            view.SelectedItems.Clear();
            item.IsSelected = true;
        }

        if (e.NewValue is bool)
            element.PreviewMouseUp += OnPreviewMouseUp;
        else
            element.PreviewMouseUp -= OnPreviewMouseUp;
    }
}
