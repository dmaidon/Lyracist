using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

/// <summary>
/// Base class for all ViewModels, inheriting from CommunityToolkit.Mvvm's ObservableObject.
/// </summary>
public abstract class BaseViewModel : ObservableObject
{
    /*
    ===========================================================================
    RelayCommand Usage Examples
    ===========================================================================
    
    1. Synchronous Command:
    
       [RelayCommand]
       private void DoSomething()
       {
           // Implementation
       }
       
       Binds to Command="{Binding DoSomethingCommand}" in XAML.
       
    2. Asynchronous Command:
    
       [RelayCommand]
       private async Task LoadDataAsync()
       {
           await Task.Delay(1000);
       }
       
       Binds to Command="{Binding LoadDataCommand}" in XAML. Can use:
       - Command="{Binding LoadDataCommand}"
       - IsEnabled="{Binding LoadDataCommand.IsRunning}" (automatically managed!)

    3. Command with Parameter:
    
       [RelayCommand]
       private void DeleteItem(string itemId)
       {
           // Delete logic
       }
       
       Binds to Command="{Binding DeleteItemCommand}" CommandParameter="{Binding CurrentItemId}" in XAML.

    4. Conditional Command Execution (CanExecute):
    
       [RelayCommand(CanExecute = nameof(CanSubmit))]
       private void Submit()
       {
           // Submit logic
       }
       
       private bool CanSubmit() => !string.IsNullOrEmpty(Username);
       
       Call username changes: SubmitCommand.NotifyCanExecuteChanged();
    */

    public void NotifyPropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }
}
