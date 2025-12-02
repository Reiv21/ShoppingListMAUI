using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList.Views
{
    public partial class ShoppingListPage : ContentPage
    {
        public ShoppingListPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // ustawiamy widok na tryb listy zakupow aby wyswietlic odpowiednie dane
            if (BindingContext is MainViewModel vm)
            {
                vm.IsShopView = false;
                vm.RefreshShopViewCommand.Execute(null);
            }
        }
    }
}
