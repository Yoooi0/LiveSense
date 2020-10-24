using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiveSense.Common.Behaviours
{
    public static class DataGridBehavior
    {
        public static readonly DependencyProperty FullRowSelectProperty = DependencyProperty.RegisterAttached("FullRowSelect",
            typeof(bool),
            typeof(DataGridBehavior),
            new UIPropertyMetadata(false, OnFullRowSelectChanged));

        public static bool GetFullRowSelect(DataGrid grid)
            => (bool)grid.GetValue(FullRowSelectProperty);

        public static void SetFullRowSelect(DataGrid grid, bool value)
            => grid.SetValue(FullRowSelectProperty, value);

        public static readonly DependencyProperty CommitRowOnCellEditEndingProperty = DependencyProperty.RegisterAttached("CommitRowOnCellEditEnding",
            typeof(bool),
            typeof(DataGridBehavior),
            new UIPropertyMetadata(false, OnCommitRowOnCellEditEndingChanged));

        public static bool GetCommitRowOnCellEditEnding(DataGrid grid)
            => (bool)grid.GetValue(CommitRowOnCellEditEndingProperty);

        public static void SetCommitRowOnCellEditEnding(DataGrid grid, bool value)
            => grid.SetValue(CommitRowOnCellEditEndingProperty, value);

        private static void OnFullRowSelectChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (!(depObj is DataGrid grid))
                return;

            if (e.NewValue is bool)
            {
                grid.SelectionMode = DataGridSelectionMode.Single;
                grid.MouseDown += OnMouseDown;
            }
            else
            {
                grid.MouseDown -= OnMouseDown;
            }
        }

        private static void OnCommitRowOnCellEditEndingChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (!(depObj is DataGrid grid))
                return;

            if (e.NewValue is bool)
                grid.CellEditEnding += OnCellEditEnding;
            else
                grid.CellEditEnding -= OnCellEditEnding;
        }


        private static bool _suppressCellEditEndingEvent;
        private static void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!(sender is DataGrid grid))
                return;

            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            if (!_suppressCellEditEndingEvent)
            {
                _suppressCellEditEndingEvent = true;
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                _suppressCellEditEndingEvent = false;
            }
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var dependencyObject = (DependencyObject)e.OriginalSource;
            while ((dependencyObject != null) && !(dependencyObject is DataGridRow))
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

            var row = dependencyObject as DataGridRow;
            if (row == null)
                return;

            row.IsSelected = true;

            dependencyObject = row;
            while ((dependencyObject != null) && !(dependencyObject is DataGrid))
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

            var dataGrid = dependencyObject as DataGrid;
            if (dataGrid == null)
                return;

            dataGrid.Focus();
        }
    }
}
