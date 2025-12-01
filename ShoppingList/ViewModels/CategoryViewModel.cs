using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ShoppingList.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace ShoppingList.ViewModels
{
    public class CategoryViewModel : BaseViewModel
    {
        private Category _model;
        public Category Model => _model;
        public Guid Id => _model.Id;
        public string Name { get => _model.Name; set { _model.Name = value; OnPropertyChanged(); } }
        public ObservableCollection<ProductViewModel> Products { get; } = new ObservableCollection<ProductViewModel>();
        public ObservableCollection<ProductViewModel> FilteredProducts { get; } = new ObservableCollection<ProductViewModel>();
        private bool _hasFilterMatch = true;
        public bool HasFilterMatch { get => _hasFilterMatch; set => SetProperty(ref _hasFilterMatch, value); }
        private string _lastFilter = string.Empty;
        private bool _isExpanded = true;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

        private string _newProductName = "";
        public string NewProductName { get => _newProductName; set => SetProperty(ref _newProductName, value); }

        private string _newProductUnit = "szt.";
        public string NewProductUnit { get => _newProductUnit; set => SetProperty(ref _newProductUnit, value); }

        private double _newProductQuantity = 1;
        public double NewProductQuantity { get => _newProductQuantity; set => SetProperty(ref _newProductQuantity, value); }

        public ICommand AddProductCommand { get; }

        // nowy: komenda do zwijania/rozwijania kategorii
        public ICommand ToggleExpandCommand { get; }

        public CategoryViewModel(Category c)
        {
            _model = c;

            Products.CollectionChanged += (s, e) => ReapplyFilter();

            AddProductCommand = new Command(() =>
            {
                var name = string.IsNullOrWhiteSpace(NewProductName) ? "Nowy produkt" : NewProductName.Trim();
                var unit = string.IsNullOrWhiteSpace(NewProductUnit) ? "szt." : NewProductUnit.Trim();
                var qty = NewProductQuantity <= 0 ? 1 : NewProductQuantity;

                var p = new Product { Name = name, Unit = unit, Quantity = qty, CategoryId = Id };
                var vm = new ProductViewModel(p);
                vm.OnDelete += (s, e) => Products.Remove(vm);
                vm.OnBoughtChanged += (s, e) => MoveBoughtToEnd(vm);
                Products.Add(vm);

                NewProductName = "";
                NewProductUnit = "szt.";
                NewProductQuantity = 1;
            });

            ToggleExpandCommand = new Command(() =>
            {
                IsExpanded = !IsExpanded;
            });

            ResetFilter();
        }

        public void ApplyFilter(string? filterText)
        {
            var term = filterText ?? _lastFilter;
            if (string.IsNullOrWhiteSpace(term))
            {
                _lastFilter = string.Empty;
                ResetFilter();
                return;
            }

            _lastFilter = term;
            term = term.Trim().ToLowerInvariant();
            var matches = Products
                .Where(p => !string.IsNullOrWhiteSpace(p.Store) && p.Store.ToLowerInvariant().Contains(term))
                .ToList();

            UpdateFilteredProducts(matches);
        }

        public void ResetFilter()
        {
            _lastFilter = string.Empty;
            UpdateFilteredProducts(Products.ToList(), true);
        }

        private void ReapplyFilter()
        {
            ApplyFilter(null);
        }

        private void UpdateFilteredProducts(System.Collections.Generic.IList<ProductViewModel> items, bool forceVisible = false)
        {
            FilteredProducts.Clear();
            foreach (var item in items) FilteredProducts.Add(item);
            HasFilterMatch = forceVisible || items.Count > 0;
        }

        public void MoveBoughtToEnd(ProductViewModel vm)
        {
            // Przenieś modyfikacje kolekcji na główny wątek i opóźnij wykonanie,
            // aby uniknąć modyfikacji kolekcji podczas obsługi CollectionChanged.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (vm.IsBought)
                    {
                        if (Products.Contains(vm))
                        {
                            Products.Remove(vm);
                            Products.Add(vm);
                        }
                    }
                    else
                    {
                        // Oblicz liczbę kupionych przed aktualizacją
                        var bought = Products.Where(p => p.IsBought).ToList();

                        if (Products.Contains(vm))
                        {
                            Products.Remove(vm);
                            var index = Products.Count - bought.Count;
                            if (index < 0) index = 0;
                            if (index > Products.Count) index = Products.Count;
                            Products.Insert(index, vm);
                        }
                    }
                }
                catch
                {
                    // w razie bardzo rzadkich wyścigów — ignoruj aby nie zablokować UI
                }
            });
        }

        public void SortProductsByName()
        {
            var ordered = Products.OrderBy(p => p.IsBought).ThenBy(p => p.Name).ToList();
            Products.Clear();
            foreach (var p in ordered) Products.Add(p);
        }

        public void SortProductsByQuantity()
        {
            var ordered = Products.OrderBy(p => p.IsBought).ThenBy(p => p.Quantity).ToList();
            Products.Clear();
            foreach (var p in ordered) Products.Add(p);
        }
    }
}
