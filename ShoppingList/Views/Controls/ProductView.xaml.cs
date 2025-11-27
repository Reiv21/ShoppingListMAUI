using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList.Views.Controls
{
    public partial class ProductView : ContentView
    {
        public ProductView()
        {
            InitializeComponent();
            // BindingContext is set externally (np. in CategoryView)
        }
    }
}
