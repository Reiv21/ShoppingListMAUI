using Microsoft.Maui;
using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList;

public partial class App : Application
{
    private readonly MainViewModel _mainViewModel;

    public App(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _ = _mainViewModel.InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}