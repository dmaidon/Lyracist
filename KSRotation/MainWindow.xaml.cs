// Edited on Oct 6, 2026 @ 12:13:00 -> Add mouse drag-and-drop event handlers for rotation list reordering
using System.Windows.Media;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using KSRotation.Services;
using WpfListViewItem = System.Windows.Controls.ListViewItem;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfGrid = System.Windows.Controls.Grid;
using WpfBorder = System.Windows.Controls.Border;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;
using WpfPopup = System.Windows.Controls.Primitives.Popup;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDragDrop = System.Windows.DragDrop;
using WpfDragDropEffects = System.Windows.DragDropEffects;

namespace KSRotation
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closed += OnMainWindowClosed;
            Loaded += OnMainWindowLoaded;
            DataContextChanged += OnMainWindowDataContextChanged;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.SingerInsertedForEditing -= OnSingerInsertedForEditing;
                vm.SingerInsertedForEditing += OnSingerInsertedForEditing;
            }
        }

        private void OnMainWindowDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.MainViewModel oldVm)
            {
                oldVm.SingerInsertedForEditing -= OnSingerInsertedForEditing;
            }
            if (e.NewValue is ViewModels.MainViewModel newVm)
            {
                newVm.SingerInsertedForEditing -= OnSingerInsertedForEditing;
                newVm.SingerInsertedForEditing += OnSingerInsertedForEditing;
            }
        }

        private void OnSingerInsertedForEditing(object? sender, Models.SingerEntry entry)
        {
            RequestFocusSingerName(entry);
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.SingerInsertedForEditing -= OnSingerInsertedForEditing;
                vm.Shutdown();
            }
            System.Windows.Application.Current.Shutdown();
        }

        private void OnPortalTitleClicked(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsDjQrVisible = !vm.IsDjQrVisible;
            }
        }

        private void OnQrCodeBorderClicked(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.IsDjQrVisible)
            {
                var popout = new Windows.DjQrCodePopoutWindow
                {
                    Owner = this,
                    DataContext = vm
                };
                popout.ShowDialog();
            }
        }

        private void OnKioskQrCodeButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                var popout = new Windows.KioskQrCodePopoutWindow
                {
                    Owner = this,
                    DataContext = vm
                };
                popout.ShowDialog();
            }
        }

        private void OnDeviceHandoffButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                var popout = new Windows.DeviceHandoffWindow
                {
                    Owner = this,
                    DataContext = vm
                };
                popout.ShowDialog();
            }
        }

        private static (WpfPopup? popup, WpfListBox? listBox) GetSuggestionControls(WpfTextBox textBox)
        {
            var popup = (textBox.Parent as WpfGrid)?.Children.OfType<WpfPopup>().FirstOrDefault();
            var listBox = (popup?.Child as WpfBorder)?.Child as WpfListBox;
            return (popup, listBox);
        }

        private void SingerNameTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not WpfTextBox textBox) return;
            textBox.SelectAll();

            var (popup, listBox) = GetSuggestionControls(textBox);
            if (popup != null && listBox != null && DataContext is ViewModels.MainViewModel vm)
            {
                vm.FilterKnownSingers(textBox.Text);
                if (vm.FilteredSingers.Count > 0)
                    popup.IsOpen = true;
            }
        }

        private void SingerNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is not WpfTextBox textBox) return;

            var (popup, _) = GetSuggestionControls(textBox);
            if (popup == null) return;

            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.FilterKnownSingers(textBox.Text);
                popup.IsOpen = vm.FilteredSingers.Count > 0 && textBox.IsFocused;
            }
        }

        private void SingerNameTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not WpfTextBox textBox) return;

            var (popup, listBox) = GetSuggestionControls(textBox);

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (popup?.IsOpen == true)
                {
                    popup.IsOpen = false;
                }
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                textBox.MoveFocus(request);
                return;
            }

            if (popup?.IsOpen != true || listBox == null) return;

            if (e.Key == Key.Down)
            {
                e.Handled = true;
                listBox.Focus();
                if (listBox.SelectedIndex < 0 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
                if (listBox.SelectedIndex >= 0)
                    (listBox.ItemContainerGenerator.ContainerFromIndex(listBox.SelectedIndex) as WpfListBoxItem)?.Focus();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                popup.IsOpen = false;
            }
        }

        private void SingerNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfTextBox textBox) return;

            var (popup, _) = GetSuggestionControls(textBox);

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var (_, lb) = GetSuggestionControls(textBox);
                if (lb?.IsKeyboardFocusWithin == true || textBox.IsFocused)
                {
                    return;
                }

                if (popup != null)
                {
                    popup.IsOpen = false;
                }

                if (textBox.DataContext is Models.SingerEntry entry && DataContext is ViewModels.MainViewModel vm)
                {
                    string rawText = textBox.Text?.Trim() ?? string.Empty;
                    string cleaned = Lyracist.Shared.NameFormatting.CleanSingerName(rawText);

                    if (string.IsNullOrWhiteSpace(cleaned))
                    {
                        // If "New Singer" was the only text in the box (or box was left empty/whitespace)
                        if (entry.IsNewPlaceholder || string.Equals(rawText, "New Singer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rawText))
                        {
                            vm.RemoveSingerDirectly(entry);
                        }
                    }
                    else
                    {
                        // If "New Singer" was present alongside more text (e.g. "New Singertom" -> "Tom")
                        entry.Name = cleaned;
                        textBox.Text = entry.Name;
                        entry.IsNewPlaceholder = false;
                    }
                }
            }));
        }

        private void SuggestionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is WpfListBox listBox && listBox.SelectedItem is string selectedName)
            {
                var popup = FindParent<WpfPopup>(listBox);
                if (popup != null)
                {
                    if (popup.PlacementTarget is WpfTextBox textBox)
                    {
                        textBox.Text = selectedName;
                        textBox.Focus();
                        popup.IsOpen = false;
                    }
                }
                listBox.SelectedItem = null;
            }
        }

        private void SuggestionsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is WpfListBox listBox)
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (listBox.SelectedItem is string selectedName)
                    {
                        var popup = FindParent<WpfPopup>(listBox);
                        if (popup != null)
                        {
                            if (popup.PlacementTarget is WpfTextBox textBox)
                            {
                                textBox.Text = selectedName;
                                textBox.Focus();
                                popup.IsOpen = false;
                            }
                        }
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    var popup = FindParent<WpfPopup>(listBox);
                    if (popup != null)
                    {
                        var textBox = popup.PlacementTarget as WpfTextBox;
                        textBox?.Focus();
                        popup.IsOpen = false;
                    }
                }
            }
        }

        private void SuggestionsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is WpfListBox listBox)
            {
                if (e.OriginalSource is DependencyObject depObj)
                {
                    var listBoxItem = FindParent<WpfListBoxItem>(depObj);
                    var selectedName = listBoxItem != null ? (listBoxItem.Content as string ?? listBoxItem.DataContext as string) : null;
                    if (string.IsNullOrEmpty(selectedName))
                    {
                        if (depObj is System.Windows.Controls.TextBlock textBlock)
                        {
                            selectedName = textBlock.Text;
                        }
                    }
                    if (!string.IsNullOrEmpty(selectedName))
                    {
                        var popup = FindParent<WpfPopup>(listBox);
                        if (popup != null)
                        {
                            if (popup.PlacementTarget is WpfTextBox textBox)
                            {
                                textBox.Text = selectedName;
                                textBox.Focus();
                                popup.IsOpen = false;
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? curr = child;
            while (curr != null)
            {
                if (curr is T parent) return parent;

                DependencyObject? logicalParent = LogicalTreeHelper.GetParent(curr);
                if (logicalParent is T logicalT) return logicalT;

                DependencyObject? visualParent = VisualTreeHelper.GetParent(curr);
                curr = visualParent ?? logicalParent;
            }
            return null;
        }

        private void SingerNameTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is WpfTextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }

        private void RequestFocusSingerName(object? targetSinger = null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => FocusSingerNameTextBox(targetSinger, retryCount: 8)));
        }

        private void AddSingerButton_Click(object sender, RoutedEventArgs e)
        {
            RequestFocusSingerName();
        }

        private void FocusSingerNameTextBox(object? targetSinger, int retryCount)
        {
            if (SingersListView.Items.Count == 0)
            {
                return;
            }

            object? targetItem = targetSinger;
            if (targetItem == null)
            {
                if (DataContext is ViewModels.MainViewModel vm && vm.LastInsertedSinger != null && vm.Singers.Contains(vm.LastInsertedSinger))
                {
                    targetItem = vm.LastInsertedSinger;
                }
                else
                {
                    targetItem = SingersListView.Items[^1];
                }
            }

            if (targetItem == null)
            {
                return;
            }

            SingersListView.SelectedItem = targetItem;
            SingersListView.ScrollIntoView(targetItem);
            SingersListView.UpdateLayout();

            if (SingersListView.ItemContainerGenerator.ContainerFromItem(targetItem) is not WpfListViewItem listViewItem)
            {
                RetryFocusSingerNameTextBox(targetSinger, retryCount);
                return;
            }

            WpfTextBox? nameTextBox = FindVisualChildByName<WpfTextBox>(listViewItem, "SingerNameInput")
                ?? FindVisualChild<WpfTextBox>(listViewItem);

            if (nameTextBox == null)
            {
                RetryFocusSingerNameTextBox(targetSinger, retryCount);
                return;
            }

            nameTextBox.Focus();
            Keyboard.Focus(nameTextBox);
            nameTextBox.SelectAll();

            // Re-assert select-all on ApplicationIdle so any trailing mouse-up or focus events cannot collapse the selection
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    if (nameTextBox.IsKeyboardFocusWithin || nameTextBox.IsFocused)
                    {
                        nameTextBox.SelectAll();
                    }
                }));
        }

        private void RetryFocusSingerNameTextBox(object? targetSinger, int retryCount)
        {
            if (retryCount <= 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => FocusSingerNameTextBox(targetSinger, retryCount - 1)));
        }

        private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                T? descendant = FindVisualChildByName<T>(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                T? descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        #region Singer Drag and Drop Reordering

        private WpfPoint _singerDragStartPoint;
        private Models.SingerEntry? _draggedSinger;

        private void SingersListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not WpfListViewItem)
            {
                if (dep is System.Windows.Controls.Primitives.TextBoxBase ||
                    dep is System.Windows.Controls.Primitives.ButtonBase ||
                    dep is System.Windows.Controls.CheckBox ||
                    dep is System.Windows.Controls.ComboBox ||
                    dep is System.Windows.Controls.Primitives.ScrollBar ||
                    dep is WpfPopup ||
                    dep is WpfListBox)
                {
                    _draggedSinger = null;
                    return;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (sender is WpfListViewItem item && item.DataContext is Models.SingerEntry singer)
            {
                _singerDragStartPoint = e.GetPosition(null);
                _draggedSinger = singer;
            }
        }

        private void SingersListViewItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedSinger = null;
        }

        private void SingersListViewItem_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedSinger != null)
            {
                WpfPoint currentPosition = e.GetPosition(null);
                Vector diff = _singerDragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is WpfListViewItem item)
                    {
                        var singer = _draggedSinger;
                        WpfDragDrop.DoDragDrop(item, singer, WpfDragDropEffects.Move);
                        _draggedSinger = null;
                    }
                }
            }
        }

        private void SingersListViewItem_DragOver(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Models.SingerEntry)))
            {
                e.Effects = WpfDragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = WpfDragDropEffects.None;
            }
        }

        private void SingersListViewItem_Drop(object sender, WpfDragEventArgs e)
        {
            if (sender is WpfListViewItem targetItem &&
                e.Data.GetData(typeof(Models.SingerEntry)) is Models.SingerEntry sourceSinger &&
                DataContext is ViewModels.MainViewModel vm)
            {
                if (targetItem.DataContext is Models.SingerEntry targetSinger && sourceSinger != targetSinger)
                {
                    int sourceIndex = vm.Singers.IndexOf(sourceSinger);
                    int targetIndex = vm.Singers.IndexOf(targetSinger);

                    if (sourceIndex >= 0 && targetIndex >= 0)
                    {
                        WpfPoint pos = e.GetPosition(targetItem);
                        bool dropAfter = pos.Y >= (targetItem.ActualHeight / 2.0);

                        int insertIndex;
                        if (dropAfter)
                        {
                            insertIndex = (sourceIndex < targetIndex) ? targetIndex : targetIndex + 1;
                        }
                        else
                        {
                            insertIndex = (sourceIndex < targetIndex) ? targetIndex - 1 : targetIndex;
                        }

                        insertIndex = Math.Clamp(insertIndex, 0, vm.Singers.Count - 1);
                        if (insertIndex != sourceIndex)
                        {
                            vm.MoveSingerToPosition(sourceSinger, insertIndex);
                        }
                    }
                }
            }
            _draggedSinger = null;
            e.Handled = true;
        }

        private void SingersListView_DragOver(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Models.SingerEntry)))
            {
                e.Effects = WpfDragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = WpfDragDropEffects.None;
            }
        }

        private void SingersListView_Drop(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetData(typeof(Models.SingerEntry)) is Models.SingerEntry sourceSinger &&
                DataContext is ViewModels.MainViewModel vm)
            {
                int sourceIndex = vm.Singers.IndexOf(sourceSinger);
                if (sourceIndex >= 0)
                {
                    vm.MoveSingerToPosition(sourceSinger, vm.Singers.Count - 1);
                }
            }
            _draggedSinger = null;
            e.Handled = true;
        }

        #endregion
    }
}