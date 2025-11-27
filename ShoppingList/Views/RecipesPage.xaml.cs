using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList.Views
{
    public partial class RecipesPage : ContentPage
    {
        public RecipesPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

