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

                    QueueRefreshShopView();
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

        private string _newIngredientCategoryName = string.Empty;
        public string NewIngredientCategoryName
        {
            get => _newIngredientCategoryName;
            set => SetProperty(ref _newIngredientCategoryName, value);
        }

        private string _newRecipeInstructions = string.Empty;
        public string NewRecipeInstructions { get => _newRecipeInstructions; set => SetProperty(ref _newRecipeInstructions, value); }

        // Picker dla kategorii przy dodawaniu składnika do przepisu
        private CategoryViewModel? _newIngredientSelectedCategory;
        public CategoryViewModel? NewIngredientSelectedCategory
        {
            get => _newIngredientSelectedCategory;
            set => SetProperty(ref _newIngredientSelectedCategory, value);
        }

        // Lista dostępnych sklepów do filtrowania
        public ObservableCollection<string> AvailableStores { get; } = new ObservableCollection<string>();

        private string? _selectedStore;
        public string? SelectedStore
        {
            get => _selectedStore;
            set
            {
                if (SetProperty(ref _selectedStore, value))
                {
                    // synchronizuj z tekstowym filtrem i zastosuj
                    ShopFilterText = value ?? string.Empty;
                }
            }
        }

        public ICommand? AddCategoryCommand { get; private set; }
        public ICommand? SaveCommand { get; private set; }
        public ICommand? ToggleShopViewCommand { get; private set; }
        public ICommand? RefreshShopViewCommand { get; private set; }
        public ICommand? ExportCommand { get; private set; }
        public ICommand? ImportCommand { get; private set; }
        public ICommand? SortByNameCommand { get; private set; }
        public ICommand? SortByQuantityCommand { get; private set; }
        public ICommand? SortByCategoryCommand { get; private set; }
        public ICommand? AddRecipeCommand { get; private set; }
        public ICommand? ImportRecipeCommand { get; private set; }
        public ICommand? AddIngredientToRecipeCommand { get; private set; }
        public ICommand? ToggleRecipeExpandCommand { get; private set; }
        public ICommand? DeleteCategoryCommand { get; private set; }
        public ICommand? DeleteRecipeCommand { get; private set; }
        public ICommand? ClearShopFilterCommand { get; private set; }

        private readonly Dictionary<CategoryViewModel, NotifyCollectionChangedEventHandler> _categoryProductsHandlers = new Dictionary<CategoryViewModel, NotifyCollectionChangedEventHandler>();
        private bool _refreshScheduled;

        public MainViewModel()
        {
            Categories.CollectionChanged += Categories_CollectionChanged;

            ShopSortOptions = CreateShopSortOptions();
            _selectedShopSortOption = ShopSortOptions[0];

            InitializeCommands();
        }

        private IReadOnlyList<ShopSortOptionEntry> CreateShopSortOptions()
        {
            return new[]
            {
                new ShopSortOptionEntry(ShopSortOption.Category, "Kategoria"),
                new ShopSortOptionEntry(ShopSortOption.Name, "Nazwa"),
                new ShopSortOptionEntry(ShopSortOption.Quantity, "Ilość")
            };
        }

        private void InitializeCommands()
        {
            InitializeCategoryCommands();
            InitializeShopViewCommands();
            InitializePersistenceCommands();
            InitializeRecipeCommands();
        }

        private void InitializeCategoryCommands()
        {
            AddCategoryCommand = new Command<string>(OnAddCategory);
            DeleteCategoryCommand = new Command<CategoryViewModel>(async c => await DeleteCategoryAsync(c));
            ClearShopFilterCommand = new Command(() => { ShopFilterText = string.Empty; SelectedStore = null; });
        }

        private void InitializeShopViewCommands()
        {
            ToggleShopViewCommand = new Command(() =>
            {
                IsShopView = !IsShopView;
                QueueRefreshShopView();
            });

            RefreshShopViewCommand = new Command(() => QueueRefreshShopView());

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
        }

        private void InitializePersistenceCommands()
        {
            SaveCommand = new Command(async () => await SaveAsync());
            ExportCommand = new Command(async () => await ExportAsync());
            ImportCommand = new Command(async () => await ImportAsync());
        }

        private void InitializeRecipeCommands()
        {
            AddRecipeCommand = new Command(async () => await AddRecipeAsync());
            ImportRecipeCommand = new Command<Recipe>(async r => await ImportRecipeAsync(r));
            AddIngredientToRecipeCommand = new Command<Recipe>(async r => await AddIngredientToRecipeAsync(r));
            DeleteRecipeCommand = new Command<Recipe>(async r => await DeleteRecipeAsync(r));
            ToggleRecipeExpandCommand = new Command<Recipe>(r =>
            {
                if (r == null) return;
                r.IsExpanded = !r.IsExpanded;
            });
        }

        private void OnAddCategory(string name)
        {
            Category category = new Category { Name = string.IsNullOrWhiteSpace(name) ? "Nowa kategoria" : name, Order = Categories.Count };
            CategoryViewModel categoryVm = new CategoryViewModel(category, SaveAsync);
            AttachCategoryHandlers(categoryVm);
            Categories.Add(categoryVm);
        }

        private async Task DeleteCategoryAsync(CategoryViewModel categoryVm)
        {
            if (categoryVm == null) return;
            DetachCategoryHandlers(categoryVm);
            Categories.Remove(categoryVm);
            await SaveAsync();
            QueueRefreshShopView();
        }

        private async Task ExportAsync()
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
        }

        private async Task ImportAsync()
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
        }

        private async Task AddRecipeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRecipeTitle)) return;

            Recipe recipe = new Recipe
            {
                Title = NewRecipeTitle.Trim(),
                Description = NewRecipeDescription?.Trim() ?? string.Empty,
                Instructions = NewRecipeInstructions?.Trim() ?? string.Empty
            };

            Recipes.Add(recipe);
            NewRecipeTitle = string.Empty;
            NewRecipeDescription = string.Empty;
            NewRecipeInstructions = string.Empty;

            await SaveAsync();
        }

        private async Task ImportRecipeAsync(Recipe recipe)
        {
            if (recipe == null) return;
            AddRecipeIngredients(recipe);
            await SaveAsync();
            QueueRefreshShopView();
        }

        private async Task AddIngredientToRecipeAsync(Recipe recipe)
        {
            Recipe? target = recipe ?? SelectedRecipe;
            if (target == null) return;

            string name = string.IsNullOrWhiteSpace(NewIngredientName) ? "Nowy składnik" : NewIngredientName.Trim();
            string unit = string.IsNullOrWhiteSpace(NewIngredientUnit) ? "szt." : NewIngredientUnit.Trim();
            double quantity = NewIngredientQuantity <= 0 ? 1 : NewIngredientQuantity;

            // priorytet: wybrana kategoria z Picker, potem pole z nazwą, inaczej puste (Inne zostanie użyte przy dodawaniu)
            Guid categoryId = Guid.Empty;
            if (NewIngredientSelectedCategory != null)
            {
                categoryId = NewIngredientSelectedCategory.Id;
            }
            else if (!string.IsNullOrWhiteSpace(NewIngredientCategoryName))
            {
                // spróbuj znaleźć istniejącą kategorię o tej nazwie
                CategoryViewModel? existingCat = Categories.FirstOrDefault(c => string.Equals(c.Name, NewIngredientCategoryName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existingCat != null)
                {
                    categoryId = existingCat.Id;
                }
            }

            Product ingredient = new Product
            {
                Name = name,
                Unit = unit,
                Quantity = quantity,
                Store = NewIngredientStore?.Trim() ?? string.Empty,
                IsOptional = NewIngredientIsOptional,
                CategoryId = categoryId
            };

            target.Ingredients.Add(ingredient);

            NewIngredientName = string.Empty;
            NewIngredientUnit = "szt.";
            NewIngredientQuantity = 1;
            NewIngredientStore = string.Empty;
            NewIngredientIsOptional = false;
            NewIngredientCategoryName = string.Empty;
            NewIngredientSelectedCategory = null;

            OnPropertyChanged(nameof(Recipes));

            await SaveAsync();
        }

        private async Task DeleteRecipeAsync(Recipe recipe)
        {
            if (recipe == null) return;
            Recipes.Remove(recipe);
            await SaveAsync();
        }

        public void AddRecipeIngredients(Recipe recipe)
        {
            foreach (Product ing in recipe.Ingredients)
            {
                CategoryViewModel categoryVm = GetOrCreateCategoryForIngredient(ing);

                // szukamy istniejącego produktu o tej samej nazwie, jednostce i kategorii
                ProductViewModel? existing = categoryVm.Products
                    .FirstOrDefault(p => string.Equals(p.Name?.Trim(), ing.Name?.Trim(), StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(p.Unit?.Trim(), ing.Unit?.Trim(), StringComparison.OrdinalIgnoreCase)
                                         && p.Model.CategoryId == categoryVm.Id);

                if (existing != null)
                {
                    // jeśli istnieje, zwiększamy ilość
                    existing.Quantity += ing.Quantity;
                    existing.IsOptional = existing.IsOptional || ing.IsOptional;
                    if (string.IsNullOrWhiteSpace(existing.Store) && !string.IsNullOrWhiteSpace(ing.Store))
                    {
                        existing.Store = ing.Store;
                    }
                }
                else
                {
                    // w przeciwnym razie tworzymy nowy produkt w tej kategorii
                    Product newP = new Product
                    {
                        Name = ing.Name,
                        Unit = ing.Unit,
                        Quantity = ing.Quantity,
                        Store = ing.Store,
                        IsOptional = ing.IsOptional,
                        IsBought = false,
                        CategoryId = categoryVm.Id
                    };

                    ProductViewModel pvm = new ProductViewModel(newP);
                    pvm.OnDelete += (s, e) =>
                    {
                        categoryVm.Products.Remove(pvm);
                        _ = SaveAsync();
                    };
                    pvm.OnBoughtChanged += (s, e) => categoryVm.MoveBoughtToEnd(pvm);
                    pvm.OnChanged += (s, e) => _ = SaveAsync();
                    categoryVm.Products.Add(pvm);
                }
            }
        }

        private CategoryViewModel GetOrCreateCategoryForIngredient(Product ingredient)
        {
            // Jeśli produkt ma już CategoryId, użyj tej kategorii (jeśli istnieje)
            if (ingredient.CategoryId != Guid.Empty)
            {
                CategoryViewModel? existingById = Categories.FirstOrDefault(c => c.Id == ingredient.CategoryId);
                if (existingById != null)
                {
                    return existingById;
                }
            }

            // Brak przypisanej kategorii — spróbuj znaleźć kategorię "Inne" lub utwórz ją
            CategoryViewModel? other = Categories.FirstOrDefault(c => string.Equals(c.Name, "Inne", StringComparison.OrdinalIgnoreCase));
            if (other != null) return other;

            Category otherModel = new Category { Name = "Inne", Order = Categories.Count };
            CategoryViewModel otherVm = new CategoryViewModel(otherModel, SaveAsync);
            AttachCategoryHandlers(otherVm);
            Categories.Add(otherVm);
            return otherVm;
        }

        // Odbuduj listę dostępnych sklepów do Picker'a
        private void RebuildAvailableStores()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var stores = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CategoryViewModel c in Categories)
                {
                    foreach (var p in c.Products)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Store)) stores.Add(p.Store.Trim());
                    }
                }

                AvailableStores.Clear();
                foreach (var s in stores.OrderBy(x => x)) AvailableStores.Add(s);
            });
        }

        // już nie utrzymujemy ręcznie pojedynczych wpisów, zawsze liczymy listę od zera w RebuildAvailableStores
        private void AddStoreToAvailable(string? store) { /* pozostawione dla kompatybilności, nie robi nic */ }

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
                    pvm.OnChanged += (s, e) => _ = SaveAsync();
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
            RebuildAvailableStores();
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
            // after saving, rebuild available stores so UI Picker reflects any newly added stores
            RebuildAvailableStores();
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
                 RebuildAvailableStores();
             };

            cvm.Products.CollectionChanged += handler;
            _categoryProductsHandlers[cvm] = handler;

            foreach (ProductViewModel p in cvm.Products)
            {
                AttachProductHandler(p);
            }

            QueueRefreshShopView();
            RebuildAvailableStores();
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
            // kiedy produkt się zmieni (np. zmieniono Store), odbuduj listę sklepów i zapisz
            pvm.OnChanged -= Product_OnChanged;
            pvm.OnChanged += Product_OnChanged;
        }

        private void DetachProductHandler(ProductViewModel pvm)
        {
            if (pvm == null) return;
            pvm.OnBoughtChanged -= Product_OnBoughtChanged;
            pvm.OnChanged -= Product_OnChanged;
        }

        private void Product_OnBoughtChanged(object? sender, EventArgs e)
        {
            QueueRefreshShopView();
        }

        private void Product_OnChanged(object? sender, EventArgs e)
        {
            RebuildAvailableStores();
            QueueRefreshShopView();
            _ = SaveAsync();
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

            string storeFilter = ShopFilterText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(storeFilter))
            {
                flatProducts = flatProducts
                    .Where(e => !string.IsNullOrWhiteSpace(e.Product.Store)
                                && string.Equals(e.Product.Store.Trim(), storeFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

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
                        List<ProductViewModel> products = flatProducts
                            .Where(e => e.Category == cat)
                            .Select(e => e.Product)
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
