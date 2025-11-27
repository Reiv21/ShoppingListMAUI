using System;
using Microsoft.Maui.Controls;
using ShoppingList.ViewModels;

namespace ShoppingList
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel vm;

        public MainPage()
        {
            InitializeComponent();
            vm = new MainViewModel();
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await vm.InitializeAsync();
        }

        private void OnAddCategoryClicked(object sender, EventArgs e)
        {
            // Znajdź Entry o nazwie "CategoryEntry" w XAML (bezpieczne, gdy pole nie istnieje)
            var entry = this.FindByName<Entry>("CategoryEntry");
            var text = entry?.Text;
            vm.AddCategoryCommand.Execute(text);
            if (entry != null) entry.Text = string.Empty;
        }

        // Dodany brakujący handler wymagany przez MainPage.xaml:
        private void OnCounterClicked(object sender, EventArgs e)
        {
            // Minimalna implementacja, aby XAML mógł poprawnie związać zdarzenie.
            // Możesz tu dodać logikę przycisku jeśli potrzebujesz.
        }
    }
}
