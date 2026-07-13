// Last Edit: Jul 01, 2026 16:55 - Marked FindParent static to resolve CA1822 warning.
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

namespace KSRotation
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closed += OnMainWindowClosed;
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.Shutdown();
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
            if (popup == null || !popup.IsOpen || listBox == null) return;

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
            if (popup == null) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var (_, lb) = GetSuggestionControls(textBox);
                if (lb != null && !lb.IsKeyboardFocusWithin && !textBox.IsFocused)
                    popup.IsOpen = false;
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

        private void AddSingerButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => FocusNewestSingerNameTextBox(retryCount: 3)));
        }

        private void FocusNewestSingerNameTextBox(int retryCount)
        {
            if (SingersListView.Items.Count == 0)
            {
                return;
            }

            object lastItem = SingersListView.Items[^1];
            SingersListView.ScrollIntoView(lastItem);
            SingersListView.UpdateLayout();

            if (SingersListView.ItemContainerGenerator.ContainerFromItem(lastItem) is not WpfListViewItem listViewItem)
            {
                RetryFocusNewestSingerNameTextBox(retryCount);
                return;
            }

            WpfTextBox? nameTextBox = FindVisualChild<WpfTextBox>(listViewItem);
            if (nameTextBox == null)
            {
                RetryFocusNewestSingerNameTextBox(retryCount);
                return;
            }

            nameTextBox.Focus();
            nameTextBox.SelectAll();
        }

        private void RetryFocusNewestSingerNameTextBox(int retryCount)
        {
            if (retryCount <= 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => FocusNewestSingerNameTextBox(retryCount - 1)));
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
    }
}