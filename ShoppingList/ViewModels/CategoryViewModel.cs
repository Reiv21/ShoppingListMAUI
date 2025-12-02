using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ShoppingList.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

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
        private readonly Func<Task>? _saveCallback;
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

        // komenda do zwijania/rozwijania kategorii
        public ICommand ToggleExpandCommand { get; }

        public CategoryViewModel(Category c, Func<Task>? saveCallback = null)
        {
            _model = c;
            _saveCallback = saveCallback;

            Products.CollectionChanged += (s, e) => ReapplyFilter();

            AddProductCommand = new Command(() =>
            {
                string name = string.IsNullOrWhiteSpace(NewProductName) ? "Nowy produkt" : NewProductName.Trim();
                string unit = string.IsNullOrWhiteSpace(NewProductUnit) ? "szt." : NewProductUnit.Trim();
                double qty = NewProductQuantity <= 0 ? 1 : NewProductQuantity;

                Product p = new Product { Name = name, Unit = unit, Quantity = qty, CategoryId = Id };
                ProductViewModel vm = new ProductViewModel(p);
                vm.OnDelete += (s, e) =>
                {
                    Products.Remove(vm);
                    RequestSave();
                };
                vm.OnBoughtChanged += (s, e) => MoveBoughtToEnd(vm);
                Products.Add(vm);

                NewProductName = string.Empty;
                NewProductUnit = "szt.";
                NewProductQuantity = 1;
                RequestSave();
            });

            ToggleExpandCommand = new Command(() =>
            {
                IsExpanded = !IsExpanded;
            });

            ResetFilter();
        }

        private void RequestSave()
        {
            if (_saveCallback != null)
            {
                _ = _saveCallback();
            }
        }

        public void ApplyFilter(string? filterText)
        {
            string term = filterText ?? _lastFilter;
            if (string.IsNullOrWhiteSpace(term))
            {
                _lastFilter = string.Empty;
                ResetFilter();
                return;
            }

            _lastFilter = term;
            term = term.Trim().ToLowerInvariant();
            System.Collections.Generic.List<ProductViewModel> matches = Products
                .Where(p => !string.IsNullOrWhiteSpace(p.Store) &&
                            p.Store.ToLowerInvariant().Contains(term))
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
            foreach (ProductViewModel item in items) FilteredProducts.Add(item);
            HasFilterMatch = forceVisible || items.Count > 0;
        }

        public void MoveBoughtToEnd(ProductViewModel vm)
        {
            // przenosi kupione pozycje na koniec kolekcji lub niekupione przed kupionymi
            // bez tworzenia nowej kolekci/zmiany referencji
            // modyfikacja kolejnosci odbywa sie tutaj gdy zmienia się stan IsBought konkretnego produktu

            // BeginInvokeOnMainThread odracza wykonanie poza biezace zdarzenie CollectionChanged,
            // co zapobiega wyjątkom typu "collection was modified" i problema z ods UI.
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
                        // liczymy kupione przed aktualizacja
                        System.Collections.Generic.List<ProductViewModel> bought = Products.Where(p => p.IsBought).ToList();

                        if (Products.Contains(vm))
                        {
                            Products.Remove(vm);
                            int index = Products.Count - bought.Count;
                            if (index < 0) index = 0;
                            if (index > Products.Count) index = Products.Count;
                            Products.Insert(index, vm);
                        }
                    }
                }
                catch
                {
                    // nie powinno wywalic, jednak nie ufam MAUI wiec jest ten catch i wszystko powinno smigac
                    // z pustym catchiem, dzieki temu sie UI nie zablokuje w razie czego
                }
            });
        }

        public void SortProductsByName()
        {
            System.Collections.Generic.List<ProductViewModel> ordered = Products.OrderBy(p => p.IsBought).ThenBy(p => p.Name).ToList();
            Products.Clear();
            foreach (ProductViewModel p in ordered) Products.Add(p);
        }

        public void SortProductsByQuantity()
        {
            System.Collections.Generic.List<ProductViewModel> ordered = Products.OrderBy(p => p.IsBought).ThenBy(p => p.Quantity).ToList();
            Products.Clear();
            foreach (ProductViewModel p in ordered) Products.Add(p);
        }
    }
}
