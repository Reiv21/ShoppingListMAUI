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
        // ustawiamy widok na tryb listy zakupow aby wyswietlic odpowiednie dane
        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is MainViewModel vm)
            {
                vm.IsShopView = true;
                vm.RefreshShopViewCommand.Execute(null);
            }
        }
        // przy opuszczaniu strony resetujemy tryb widoku
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BindingContext is MainViewModel vm)
            {
                vm.IsShopView = false;
            }
        }
    }
}
