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
            if (BindingContext is MainViewModel vm)
            {
                vm.IsShopView = false;
                // na tej stronie nie musimy odświeżać widoku sklepu, więc nie wywołujemy RefreshShopViewCommand
            }
        }
    }
}
