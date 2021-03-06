using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiveSense.Common.Behaviours
{
    public static class DataGridBehavior
    {
        public static readonly DependencyProperty FullRowSelectProperty =
            DependencyProperty.RegisterAttached("FullRowSelect",
                typeof(bool), typeof(DataGridBehavior),
                    new UIPropertyMetadata(false, OnFullRowSelectChanged));

        public static bool GetFullRowSelect(DataGrid grid)
            => (bool)grid.GetValue(FullRowSelectProperty);

        public static void SetFullRowSelect(DataGrid grid, bool value)
            => grid.SetValue(FullRowSelectProperty, value);

        public static readonly DependencyProperty CommitRowOnCellEditEndingProperty =
            DependencyProperty.RegisterAttached("CommitRowOnCellEditEnding",
                typeof(bool), typeof(DataGridBehavior),
                    new UIPropertyMetadata(false, OnCommitRowOnCellEditEndingChanged));

        public static bool GetCommitRowOnCellEditEnding(DataGrid grid)
            => (bool)grid.GetValue(CommitRowOnCellEditEndingProperty);

        public static void SetCommitRowOnCellEditEnding(DataGrid grid, bool value)
            => grid.SetValue(CommitRowOnCellEditEndingProperty, value);

        public static readonly DependencyProperty CommitCellOnLostFocusProperty =
            DependencyProperty.RegisterAttached("CommitCellOnLostFocus",
                typeof(bool), typeof(DataGridBehavior),
                    new UIPropertyMetadata(false, OnCommitCellOnLostFocusChanged));

        public static bool GetCommitCellOnLostFocus(DataGrid grid)
            => (bool)grid.GetValue(CommitCellOnLostFocusProperty);

        public static void SetCommitCellOnLostFocus(DataGrid grid, bool value)
            => grid.SetValue(CommitCellOnLostFocusProperty, value);

        public static readonly DependencyProperty SelectRowOnCellEditProperty =
            DependencyProperty.RegisterAttached("SelectRowOnCellEdit",
                typeof(bool), typeof(DataGridBehavior),
                    new UIPropertyMetadata(false, OnSelectRowOnCellEditChanged));

        public static bool GetSelectRowOnCellEdit(DataGrid grid)
            => (bool)grid.GetValue(SelectRowOnCellEditProperty);

        public static void SetSelectRowOnCellEdit(DataGrid grid, bool value)
            => grid.SetValue(SelectRowOnCellEditProperty, value);

        private static void OnFullRowSelectChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (depObj is not DataGrid grid)
                return;

            static void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                var dependencyObject = (DependencyObject)e.OriginalSource;
                while (dependencyObject != null && dependencyObject is not DataGridRow)
                    dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

                if (dependencyObject is not DataGridRow row)
                    return;

                row.IsSelected = true;

                dependencyObject = row;
                while (dependencyObject != null && dependencyObject is not DataGrid)
                    dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

                if (dependencyObject is not DataGrid dataGrid)
                    return;

                dataGrid.Focus();
            }

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

        private static bool _suppressCellEditEndingEvent;
        private static void OnCommitRowOnCellEditEndingChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (depObj is not DataGrid grid)
                return;

            static void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
            {
                if (sender is not DataGrid grid)
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

            if (e.NewValue is bool)
                grid.CellEditEnding += OnCellEditEnding;
            else
                grid.CellEditEnding -= OnCellEditEnding;
        }

        private static void OnCommitCellOnLostFocusChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (depObj is not DataGrid grid)
                return;

            void OnWindowMouseUp(object sender, MouseEventArgs e)
            {
                if (sender is not Window window)
                    return;

                var dependencyObject = (DependencyObject)e.OriginalSource;
                while (dependencyObject != null && dependencyObject is not DataGrid)
                    dependencyObject = VisualTreeHelper.GetParent(dependencyObject);

                if (dependencyObject is DataGrid)
                    return;

                grid.CommitEdit();
                grid.CancelEdit();

                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(window), null);
                Keyboard.ClearFocus();
            }

            var window = Application.Current.MainWindow;
            window.MouseUp -= OnWindowMouseUp;
            window.MouseUp += OnWindowMouseUp;
        }

        private static void OnSelectRowOnCellEditChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            if (depObj is not DataGrid grid)
                return;

            static void OnPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
            {
                if (sender is not DataGrid grid)
                    return;

                grid.SelectedIndex = e.Row.GetIndex();
            }

            grid.PreparingCellForEdit -= OnPreparingCellForEdit;
            grid.PreparingCellForEdit += OnPreparingCellForEdit;
        }
    }
}
