using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LiveSense.Common.Behaviours
{
    public class ListBoxBehaviour
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached("SelectedItems", typeof(IList), typeof(ListBoxBehaviour),
                new PropertyMetadata(default(IList), OnAttach));

        public static void SetSelectedItems(DependencyObject d, IList value)
        {
            d.SetValue(SelectedItemsProperty, value);
        }

        public static IList GetSelectedItems(DependencyObject d)
        {
            return (IList)d.GetValue(SelectedItemsProperty);
        }

        private static void OnAttach(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selectedItems = default(IList);
            {
                if (d is MultiSelector multiSelector)
                {
                    multiSelector.SelectionChanged -= OnSelectionChanged;
                    selectedItems = multiSelector.SelectedItems;
                }

                if (d is ListBox listBox)
                {
                    listBox.SelectionChanged -= OnSelectionChanged;
                    selectedItems = listBox.SelectedItems;
                }
            }

            if (selectedItems != null)
            {
                if(e.OldValue is IList oldItems)
                    foreach (var item in oldItems)
                        selectedItems.Remove(item);

                if (e.NewValue is IList newItems)
                    foreach (var item in newItems)
                        selectedItems.Add(item);
            }

            {
                if (d is MultiSelector multiSelector)
                    multiSelector.SelectionChanged += OnSelectionChanged;

                if (d is ListBox listBox)
                    listBox.SelectionChanged += OnSelectionChanged;
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var list = GetSelectedItems((DependencyObject)sender);
            foreach (var item in e.RemovedItems)
                list.Remove(item);

            foreach (var item in e.AddedItems)
                list.Add(item);
        }
    }
}
