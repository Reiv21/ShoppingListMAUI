using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ShoppingList.Models;
using ShoppingList.Services;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;

namespace ShoppingList.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly DataService _dataService = new DataService();
        private AppData? _data;

        private bool _isInitializing = true;
        public bool IsInitializing
        {
            get => _isInitializing;
            private set => SetProperty(ref _isInitializing, value);
        }

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            private set => SetProperty(ref _isInitialized, value);
        }

        public ObservableCollection<CategoryViewModel> Categories { get; } = new ObservableCollection<CategoryViewModel>();
        private string _shopFilterText = string.Empty;
        public string ShopFilterText
        {
            get => _shopFilterText;
            set
            {
                if (SetProperty(ref _shopFilterText, value))
                {
                    string? filter = value?.Trim();
                    if (string.IsNullOrWhiteSpace(filter))
                    {
                        foreach (CategoryViewModel category in Categories)
                        {
                            category.ResetFilter();
                        }
                    }
                    else
                    {
                        ApplyShopFilter();
                    }
                }
            }
        }

        public class ProductGroup : ObservableCollection<ProductViewModel>
        {
            public string CategoryName { get; }
            public int Order { get; }

            public ProductGroup(string name, int order, IEnumerable<ProductViewModel> items) : base(items)
            {
                CategoryName = name;
                Order = order;
            }
        }

        public ObservableCollection<ProductGroup> ShopViewGroups { get; } = new ObservableCollection<ProductGroup>();
        public ObservableCollection<ProductViewModel> ShopViewProducts { get; } = new ObservableCollection<ProductViewModel>();
        public ObservableCollection<Recipe> Recipes { get; } = new ObservableCollection<Recipe>();

        public enum ShopSortOption
        {
            Category,
            Name,
            Quantity
        }

        public sealed class ShopSortOptionEntry
        {
            public ShopSortOption Option { get; }
            public string Label { get; }

            public ShopSortOptionEntry(ShopSortOption option, string label)
            {
                Option = option;
                Label = label;
            }
        }

        public IReadOnlyList<ShopSortOptionEntry> ShopSortOptions { get; }

        private ShopSortOptionEntry _selectedShopSortOption;
        public ShopSortOptionEntry SelectedShopSortOption
        {
            get => _selectedShopSortOption;
            set
            {
                if (SetProperty(ref _selectedShopSortOption, value))
                {
                    QueueRefreshShopView();
                }
            }
        }

        private Recipe? _selectedRecipe;
        public Recipe? SelectedRecipe
        {
            get => _selectedRecipe;
            set => SetProperty(ref _selectedRecipe, value);
        }

        private bool _isShopView;
        public bool IsShopView { get => _isShopView; set => SetProperty(ref _isShopView, value); }

        private string _newRecipeTitle = string.Empty;
        public string NewRecipeTitle { get => _newRecipeTitle; set => SetProperty(ref _newRecipeTitle, value); }

        private string _newRecipeDescription = string.Empty;
        public string NewRecipeDescription { get => _newRecipeDescription; set => SetProperty(ref _newRecipeDescription, value); }

        private string _newIngredientName = string.Empty;
        public string NewIngredientName
        {
            get => _newIngredientName;
            set => SetProperty(ref _newIngredientName, value);
        }

        private string _newIngredientUnit = "szt.";
        public string NewIngredientUnit
        {
            get => _newIngredientUnit;
            set => SetProperty(ref _newIngredientUnit, value);
        }

        private double _newIngredientQuantity = 1;
        public double NewIngredientQuantity
        {
            get => _newIngredientQuantity;
            set => SetProperty(ref _newIngredientQuantity, value);
        }

        private string _newIngredientStore = string.Empty;
        public string NewIngredientStore
        {
            get => _newIngredientStore;
            set => SetProperty(ref _newIngredientStore, value);
        }

        private bool _newIngredientIsOptional;
        public bool NewIngredientIsOptional
        {
            get => _newIngredientIsOptional;
            set => SetProperty(ref _newIngredientIsOptional, value);
        }

        public ICommand AddCategoryCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ToggleShopViewCommand { get; }
        public ICommand RefreshShopViewCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand SortByNameCommand { get; }
        public ICommand SortByQuantityCommand { get; }
        public ICommand SortByCategoryCommand { get; }
        public ICommand AddRecipeCommand { get; }
        public ICommand ImportRecipeCommand { get; }
        public ICommand AddIngredientToRecipeCommand { get; }
        public ICommand ToggleRecipeExpandCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand DeleteRecipeCommand { get; }
        public ICommand ClearShopFilterCommand { get; }

        private readonly Dictionary<CategoryViewModel, NotifyCollectionChangedEventHandler> _categoryProductsHandlers = new Dictionary<CategoryViewModel, NotifyCollectionChangedEventHandler>();
        private bool _refreshScheduled;

        public MainViewModel()
        {
            Categories.CollectionChanged += Categories_CollectionChanged;

            ShopSortOptions = new[]
            {
                new ShopSortOptionEntry(ShopSortOption.Category, "Kategoria"),
                new ShopSortOptionEntry(ShopSortOption.Name, "Nazwa"),
                new ShopSortOptionEntry(ShopSortOption.Quantity, "Ilość")
            };
            _selectedShopSortOption = ShopSortOptions[0];

            AddCategoryCommand = new Command<string>(name =>
            {
                Category category = new Category { Name = string.IsNullOrWhiteSpace(name) ? "Nowa kategoria" : name, Order = Categories.Count };
                CategoryViewModel categoryVm = new CategoryViewModel(category, SaveAsync);
                AttachCategoryHandlers(categoryVm);
                Categories.Add(categoryVm);
            });

            SaveCommand = new Command(async () => await SaveAsync());

            ToggleShopViewCommand = new Command(() =>
            {
                IsShopView = !IsShopView;
                QueueRefreshShopView();
            });

            ClearShopFilterCommand = new Command(() => ShopFilterText = string.Empty);

            RefreshShopViewCommand = new Command(() => QueueRefreshShopView());

            ExportCommand = new Command(async () =>
            {
                try
                {
                    AppData data = BuildAppDataFromVm();
                    string json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    string fileName = $"lista_zakupow_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                    FileSaverResult result = await FileSaver.Default.SaveAsync(fileName, stream, default);

                    if (result.IsSuccessful)
                    {
                        await Application.Current.MainPage.DisplayAlert("Eksport", $"Zapisano plik: {result.FilePath}", "OK");
                    }
                    else if (result.Exception is not null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Eksport", $"Błąd zapisu: {result.Exception.Message}", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Eksport", $"Błąd: {ex.Message}", "OK");
                }
            });

            ImportCommand = new Command(async () =>
            {
                try
                {
                    FilePickerFileType customTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { "*.json" } },
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.iOS, new[] { "public.json" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                    });

                    FileResult? pickResult = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = "Wybierz plik listy zakupów",
                        FileTypes = customTypes
                    });

                    if (pickResult == null) return;

                    ImportMode? mode = await PromptImportModeAsync();
                    if (mode == null) return;

                    using Stream stream = await pickResult.OpenReadAsync();
                    using StreamReader reader = new StreamReader(stream);
                    string json = await reader.ReadToEndAsync();

                    AppData imported = System.Text.Json.JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
                    AppData current = BuildAppDataFromVm();
                    AppData merged = _dataService.Merge(current, imported, mode.Value);

                    await LoadFromDataAsync(merged);
                    await SaveAsync();

                    string confirmation = mode == ImportMode.Replace
                        ? "Bieżące dane zostały nadpisane danymi z pliku."
                        : "Elementy z pliku zostały dodane do istniejącej listy.";

                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Import", confirmation, "OK");
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Import", $"Błąd importu: {ex.Message}", "OK");
                }
            });

            SortByNameCommand = new Command(() =>
            {
                foreach (CategoryViewModel c in Categories) c.SortProductsByName();
                QueueRefreshShopView();
            });

            SortByQuantityCommand = new Command(() =>
            {
                foreach (CategoryViewModel c in Categories) c.SortProductsByQuantity();
                QueueRefreshShopView();
            });

            SortByCategoryCommand = new Command(() =>
            {
                List<CategoryViewModel> ordered = Categories.OrderBy(c => c.Name).ToList();
                Categories.Clear();
                foreach (CategoryViewModel c in ordered) Categories.Add(c);
                QueueRefreshShopView();
            });

            AddRecipeCommand = new Command(async () =>
            {
                if (string.IsNullOrWhiteSpace(NewRecipeTitle)) return;

                Recipe recipe = new Recipe
                {
                    Title = NewRecipeTitle.Trim(),
                    Description = NewRecipeDescription?.Trim() ?? string.Empty
                };

                Recipes.Add(recipe);
                NewRecipeTitle = string.Empty;
                NewRecipeDescription = string.Empty;

                await SaveAsync();
            });

            ImportRecipeCommand = new Command<Recipe>(async recipe =>
            {
                if (recipe == null) return;

                CategoryViewModel? category = GetOrCreateCategoryForRecipe(recipe.Title);
                if (category == null) return;

                AddRecipeIngredientsToCategory(recipe, category.Id);
                await SaveAsync();
                QueueRefreshShopView();
            });

            AddIngredientToRecipeCommand = new Command<Recipe>(async recipe =>
            {
                Recipe? target = recipe ?? SelectedRecipe;
                if (target == null) return;

                string name = string.IsNullOrWhiteSpace(NewIngredientName) ? "Nowy składnik" : NewIngredientName.Trim();
                string unit = string.IsNullOrWhiteSpace(NewIngredientUnit) ? "szt." : NewIngredientUnit.Trim();
                double quantity = NewIngredientQuantity <= 0 ? 1 : NewIngredientQuantity;

                Product ingredient = new Product
                {
                    Name = name,
                    Unit = unit,
                    Quantity = quantity,
                    Store = NewIngredientStore?.Trim() ?? string.Empty,
                    IsOptional = NewIngredientIsOptional
                };

                target.Ingredients.Add(ingredient);

                NewIngredientName = string.Empty;
                NewIngredientUnit = "szt.";
                NewIngredientQuantity = 1;
                NewIngredientStore = string.Empty;
                NewIngredientIsOptional = false;

                OnPropertyChanged(nameof(Recipes));

                await SaveAsync();
            });

            DeleteCategoryCommand = new Command<CategoryViewModel>(async categoryVm =>
            {
                if (categoryVm == null) return;
                DetachCategoryHandlers(categoryVm);
                Categories.Remove(categoryVm);
                await SaveAsync();
                QueueRefreshShopView();
            });

            DeleteRecipeCommand = new Command<Recipe>(async recipe =>
            {
                if (recipe == null) return;
                Recipes.Remove(recipe);
                await SaveAsync();
            });

            ToggleRecipeExpandCommand = new Command<Recipe>(recipe =>
            {
                if (recipe == null) return;
                recipe.IsExpanded = !recipe.IsExpanded;
            });
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitializing = true;
            try
            {
                _data = await _dataService.LoadAsync();
                await LoadFromDataAsync(_data);
                IsInitialized = true;
            }
            finally
            {
                IsInitializing = false;
            }
        }

        private async Task LoadFromDataAsync(AppData data)
        {
            _data = data;
            foreach (CategoryViewModel existing in Categories.ToList())
            {
                DetachCategoryHandlers(existing);
            }

            Categories.Clear();
            Recipes.Clear();

            foreach (Category c in data.Categories.OrderBy(c => c.Order))
            {
                CategoryViewModel cvm = new CategoryViewModel(c, SaveAsync);
                List<Product> products = data.Products.Where(p => p.CategoryId == c.Id).ToList();
                foreach (Product p in products)
                {
                    ProductViewModel pvm = new ProductViewModel(p);
                    pvm.OnDelete += (s, e) =>
                    {
                        cvm.Products.Remove(pvm);
                        _ = SaveAsync();
                    };
                    pvm.OnBoughtChanged += (s, e) => cvm.MoveBoughtToEnd(pvm);
                    cvm.Products.Add(pvm);
                }
                Categories.Add(cvm);
            }

            foreach (Recipe recipe in data.Recipes)
            {
                Recipes.Add(recipe);
            }

            AttachHandlersForAllCategories();
            QueueRefreshShopView();
            ApplyShopFilter();
        }

        private void AttachHandlersForAllCategories()
        {
            foreach (CategoryViewModel c in Categories)
            {
                DetachCategoryHandlers(c);
                AttachCategoryHandlers(c);
            }
        }

        private AppData BuildAppDataFromVm()
        {
            AppData data = new AppData();
            foreach (CategoryViewModel c in Categories)
            {
                data.Categories.Add(c.Model);
                foreach (ProductViewModel p in c.Products)
                {
                    data.Products.Add(p.Model);
                }
            }

            foreach (Recipe r in Recipes)
            {
                data.Recipes.Add(r);
            }

            return data;
        }

        public async Task SaveAsync()
        {
            AppData data = BuildAppDataFromVm();
            await _dataService.SaveAsync(data);
        }

        private void Categories_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CategoryViewModel c in e.NewItems) AttachCategoryHandlers(c);
            }
            if (e.OldItems != null)
            {
                foreach (CategoryViewModel c in e.OldItems) DetachCategoryHandlers(c);
            }
            QueueRefreshShopView();
        }

        private void AttachCategoryHandlers(CategoryViewModel cvm)
        {
            if (cvm == null)
            {
                return;
            }

            if (_categoryProductsHandlers.ContainsKey(cvm))
            {
                return;
            }

            NotifyCollectionChangedEventHandler handler = (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ProductViewModel p in e.NewItems) AttachProductHandler(p);
                }

                if (e.OldItems != null)
                {
                    foreach (ProductViewModel p in e.OldItems) DetachProductHandler(p);
                }

                QueueRefreshShopView();
                ApplyShopFilter();
            };

            cvm.Products.CollectionChanged += handler;
            _categoryProductsHandlers[cvm] = handler;

            foreach (ProductViewModel p in cvm.Products)
            {
                AttachProductHandler(p);
            }

            QueueRefreshShopView();
        }

        private void DetachCategoryHandlers(CategoryViewModel cvm)
        {
            if (cvm == null)
            {
                return;
            }

            if (_categoryProductsHandlers.TryGetValue(cvm, out NotifyCollectionChangedEventHandler? handler))
            {
                cvm.Products.CollectionChanged -= handler;
                _categoryProductsHandlers.Remove(cvm);
            }

            foreach (ProductViewModel p in cvm.Products)
            {
                DetachProductHandler(p);
            }
        }

        private void AttachProductHandler(ProductViewModel pvm)
        {
            if (pvm == null) return;
            pvm.OnBoughtChanged -= Product_OnBoughtChanged;
            pvm.OnBoughtChanged += Product_OnBoughtChanged;
        }

        private void DetachProductHandler(ProductViewModel pvm)
        {
            if (pvm == null) return;
            pvm.OnBoughtChanged -= Product_OnBoughtChanged;
        }

        private void Product_OnBoughtChanged(object? sender, EventArgs e)
        {
            QueueRefreshShopView();
        }

        private void QueueRefreshShopView()
        {
            if (_refreshScheduled)
            {
                return;
            }

            _refreshScheduled = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    RefreshShopView();
                }
                finally
                {
                    _refreshScheduled = false;
                }
            });
        }

        private void RefreshShopView()
        {
            ShopViewGroups.Clear();
            ShopViewProducts.Clear();
            if (!IsShopView) return;

            List<CategoryViewModel> orderedCategories = Categories
                .OrderBy(c => c.Model?.Order ?? 0)
                .ThenBy(c => c.Name)
                .ToList();

            List<(CategoryViewModel Category, ProductViewModel Product)> flatProducts = orderedCategories
                .SelectMany(c => c.Products
                    .Where(p => !p.IsBought)
                    .Select(p => new { Category = c, Product = p }))
                .Select(x => (x.Category, x.Product))
                .ToList();

            switch (SelectedShopSortOption?.Option ?? ShopSortOption.Category)
            {
                case ShopSortOption.Name:
                    foreach ((CategoryViewModel Category, ProductViewModel Product) entry in flatProducts
                        .OrderBy(e => e.Product.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.Category.Name, StringComparer.CurrentCultureIgnoreCase))
                    {
                        ShopViewProducts.Add(entry.Product);
                    }
                    break;
                case ShopSortOption.Quantity:
                    foreach ((CategoryViewModel Category, ProductViewModel Product) entry in flatProducts
                        .OrderByDescending(e => e.Product.Quantity)
                        .ThenBy(e => e.Product.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.Category.Name, StringComparer.CurrentCultureIgnoreCase))
                    {
                        ShopViewProducts.Add(entry.Product);
                    }
                    break;
                default:
                    foreach (CategoryViewModel cat in orderedCategories)
                    {
                        List<ProductViewModel> products = cat.Products
                            .Where(p => !p.IsBought)
                            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                            .ToList();
                        if (!products.Any()) continue;

                        ProductGroup group = new ProductGroup(cat.Name, cat.Model?.Order ?? 0, products);
                        ShopViewGroups.Add(group);
                        foreach (ProductViewModel p in products) ShopViewProducts.Add(p);
                    }
                    break;
            }

            if ((SelectedShopSortOption?.Option ?? ShopSortOption.Category) != ShopSortOption.Category)
            {
                ShopViewGroups.Clear();
            }
        }

        private CategoryViewModel? GetOrCreateCategoryForRecipe(string title)
        {
            string baseName = string.IsNullOrWhiteSpace(title) ? "Przepis" : title.Trim();

            CategoryViewModel? existingExact = Categories.FirstOrDefault(c => string.Equals(c.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (existingExact == null)
            {
                Category category = new Category
                {
                    Name = baseName,
                    Order = Categories.Count
                };
                CategoryViewModel vm = new CategoryViewModel(category, SaveAsync);
                AttachCategoryHandlers(vm);
                Categories.Add(vm);
                return vm;
            }

            List<string?> sameBase = Categories
                .Select(c => c.Name)
                .Where(n => n != null && (string.Equals(n, baseName, StringComparison.OrdinalIgnoreCase) || n.StartsWith(baseName + " - ", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int maxSuffix = 0;
            foreach (string? name in sameBase)
            {
                if (string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    if (maxSuffix < 0)
                    {
                        maxSuffix = 0;
                    }
                    continue;
                }

                string parts = name.Substring(baseName.Length).TrimStart();
                if (parts.StartsWith("- ", StringComparison.Ordinal))
                {
                    string numberPart = parts.Substring(2);
                    if (int.TryParse(numberPart, out int n))
                    {
                        if (n > maxSuffix) maxSuffix = n;
                    }
                }
            }

            string newName = maxSuffix == 0 ? $"{baseName} - 1" : $"{baseName} - {maxSuffix + 1}";

            Category newCategory = new Category
            {
                Name = newName,
                Order = Categories.Count
            };
            CategoryViewModel newVm = new CategoryViewModel(newCategory, SaveAsync);
            AttachCategoryHandlers(newVm);
            Categories.Add(newVm);
            return newVm;
        }

        public void AddRecipeIngredientsToCategory(Recipe recipe, Guid categoryId)
        {
            CategoryViewModel? cat = Categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            foreach (Product ing in recipe.Ingredients)
            {
                Product newP = new Product
                {
                    Name = ing.Name,
                    Unit = ing.Unit,
                    Quantity = ing.Quantity,
                    Store = ing.Store,
                    IsOptional = ing.IsOptional,
                    IsBought = false,
                    CategoryId = categoryId
                };

                ProductViewModel pvm = new ProductViewModel(newP);
                pvm.OnDelete += (s, e) =>
                {
                    cat.Products.Remove(pvm);
                    _ = SaveAsync();
                };
                pvm.OnBoughtChanged += (s, e) => cat.MoveBoughtToEnd(pvm);
                cat.Products.Add(pvm);
            }
            _ = SaveAsync();
        }

        private void ApplyShopFilter()
        {
            string? filter = ShopFilterText?.Trim();
            foreach (CategoryViewModel category in Categories)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    category.ResetFilter();
                    continue;
                }

                category.ApplyFilter(filter);
            }
        }

        private async Task<ImportMode?> PromptImportModeAsync()
        {
            Page? page = Application.Current?.MainPage;
            if (page == null) return null;

            string choice = await page.DisplayActionSheet(
                "Jak chcesz zaimportować dane?",
                "Anuluj",
                null,
                "Nadpisz bieżące dane",
                "Dodaj do istniejących");

            return choice switch
            {
                "Nadpisz bieżące dane" => ImportMode.Replace,
                "Dodaj do istniejących" => ImportMode.Merge,
                _ => (ImportMode?)null
            };
        }
    }
}
