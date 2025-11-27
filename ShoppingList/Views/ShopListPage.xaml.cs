using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList.Views
{
    public partial class ShopListPage : ContentPage
    {
        public ShopListPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is MainViewModel vm)
            {
                vm.IsShopView = true;
                vm.RefreshShopViewCommand.Execute(null);
            }
        }
    }
}
