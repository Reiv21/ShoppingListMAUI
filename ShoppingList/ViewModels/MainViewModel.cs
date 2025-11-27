using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ShoppingList.Models;
using ShoppingList.Services;
using System.Collections.Specialized;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;

namespace ShoppingList.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly DataService _dataService = new DataService();
        private AppData? _data;

        public ObservableCollection<CategoryViewModel> Categories { get; } = new();
        public ObservableCollection<ProductViewModel> ShopViewProducts { get; } = new();
        public ObservableCollection<Recipe> Recipes { get; } = new();

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

        public MainViewModel()
        {
            Categories.CollectionChanged += Categories_CollectionChanged;

            AddCategoryCommand = new Command<string>(name =>
            {
                var category = new Category { Name = string.IsNullOrWhiteSpace(name) ? "Nowa kategoria" : name, Order = Categories.Count };
                var categoryVm = new CategoryViewModel(category);
                AttachCategoryHandlers(categoryVm);
                Categories.Add(categoryVm);
            });

            SaveCommand = new Command(async () => await SaveAsync());

            ToggleShopViewCommand = new Command(() =>
            {
                IsShopView = !IsShopView;
                RefreshShopView();
            });

            RefreshShopViewCommand = new Command(RefreshShopView);

            ExportCommand = new Command(async () =>
            {
                try
                {
                    var data = BuildAppDataFromVm();
                    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    var fileName = $"lista_zakupow_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                    var result = await FileSaver.Default.SaveAsync(fileName, stream, default);

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
                    var customTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { "*.json" } },
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.iOS, new[] { "public.json" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                    });

                    var pickResult = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = "Wybierz plik listy zakupów",
                        FileTypes = customTypes
                    });

                    if (pickResult == null) return;

                    using var stream = await pickResult.OpenReadAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    var json = await reader.ReadToEndAsync();

                    var imported = System.Text.Json.JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
                    await LoadFromDataAsync(imported);

                    await Application.Current.MainPage.DisplayAlert("Import", "Dane zostały zaimportowane.", "OK");
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Import", $"Błąd importu: {ex.Message}", "OK");
                }
            });

            SortByNameCommand = new Command(() =>
            {
                foreach (var c in Categories) c.SortProductsByName();
                RefreshShopView();
            });

            SortByQuantityCommand = new Command(() =>
            {
                foreach (var c in Categories) c.SortProductsByQuantity();
                RefreshShopView();
            });

            SortByCategoryCommand = new Command(() =>
            {
                var ordered = Categories.OrderBy(c => c.Name).ToList();
                Categories.Clear();
                foreach (var c in ordered) Categories.Add(c);
                RefreshShopView();
            });

            AddRecipeCommand = new Command(async () =>
            {
                if (string.IsNullOrWhiteSpace(NewRecipeTitle)) return;

                var recipe = new Recipe
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

                var category = GetOrCreateCategoryForRecipe(recipe.Title);
                if (category == null) return;

                AddRecipeIngredientsToCategory(recipe, category.Id);
                await SaveAsync();
                RefreshShopView();
            });

            AddIngredientToRecipeCommand = new Command<Recipe>(async recipe =>
            {
                var target = recipe ?? SelectedRecipe;
                if (target == null) return;

                var name = string.IsNullOrWhiteSpace(NewIngredientName) ? "Nowy składnik" : NewIngredientName.Trim();
                var unit = string.IsNullOrWhiteSpace(NewIngredientUnit) ? "szt." : NewIngredientUnit.Trim();
                var quantity = NewIngredientQuantity <= 0 ? 1 : NewIngredientQuantity;

                var ingredient = new Product
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
                Categories.Remove(categoryVm);
                await SaveAsync();
                RefreshShopView();
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
            _data = await _dataService.LoadAsync();
            await LoadFromDataAsync(_data);
            foreach (var c in Categories) AttachCategoryHandlers(c);
        }

        private async Task LoadFromDataAsync(AppData data)
        {
            Categories.Clear();
            Recipes.Clear();

            foreach (var c in data.Categories.OrderBy(c => c.Order))
            {
                var cvm = new CategoryViewModel(c);
                var products = data.Products.Where(p => p.CategoryId == c.Id).ToList();
                foreach (var p in products)
                {
                    var pvm = new ProductViewModel(p);
                    pvm.OnDelete += (s, e) => cvm.Products.Remove(pvm);
                    pvm.OnBoughtChanged += (s, e) => cvm.MoveBoughtToEnd(pvm);
                    cvm.Products.Add(pvm);
                }
                Categories.Add(cvm);
            }

            foreach (var recipe in data.Recipes)
            {
                Recipes.Add(recipe);
            }

            RefreshShopView();
        }

        private AppData BuildAppDataFromVm()
        {
            var data = new AppData();
            foreach (var c in Categories)
            {
                data.Categories.Add(c.Model);
                foreach (var p in c.Products)
                {
                    data.Products.Add(p.Model);
                }
            }

            foreach (var r in Recipes)
            {
                data.Recipes.Add(r);
            }

            return data;
        }

        public async Task SaveAsync()
        {
            var data = BuildAppDataFromVm();
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
            RefreshShopView();
        }

        private void AttachCategoryHandlers(CategoryViewModel cvm)
        {
            if (cvm == null) return;

            cvm.Products.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                    foreach (ProductViewModel p in e.NewItems) AttachProductHandler(p);

                if (e.OldItems != null)
                    foreach (ProductViewModel p in e.OldItems) DetachProductHandler(p);

                RefreshShopView();
            };

            foreach (var p in cvm.Products) AttachProductHandler(p);

            RefreshShopView();
        }

        private void DetachCategoryHandlers(CategoryViewModel cvm)
        {
            if (cvm == null) return;
            foreach (var p in cvm.Products) DetachProductHandler(p);
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
            RefreshShopView();
        }

        private void RefreshShopView()
        {
            ShopViewProducts.Clear();
            if (!IsShopView) return;

            var tmp = new List<(ProductViewModel product, int catOrder, string catName)>();
            foreach (var c in Categories)
            {
                var order = c.Model?.Order ?? 0;
                var name = c.Name ?? "";
                foreach (var p in c.Products.Where(p => !p.IsBought))
                {
                    tmp.Add((p, order, name));
                }
            }

            var ordered = tmp.OrderBy(x => x.catOrder).ThenBy(x => x.catName).ThenBy(x => x.product.Name).Select(x => x.product).ToList();
            foreach (var p in ordered) ShopViewProducts.Add(p);
        }

        private CategoryViewModel? GetOrCreateCategoryForRecipe(string title)
        {
            var baseName = string.IsNullOrWhiteSpace(title) ? "Przepis" : title.Trim();

            var existingExact = Categories.FirstOrDefault(c => string.Equals(c.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (existingExact == null)
            {
                var category = new Category
                {
                    Name = baseName,
                    Order = Categories.Count
                };
                var vm = new CategoryViewModel(category);
                AttachCategoryHandlers(vm);
                Categories.Add(vm);
                return vm;
            }

            var sameBase = Categories
                .Select(c => c.Name)
                .Where(n => n != null && (string.Equals(n, baseName, StringComparison.OrdinalIgnoreCase) || n.StartsWith(baseName + " - ", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var maxSuffix = 0;
            foreach (var name in sameBase)
            {
                if (string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    maxSuffix = Math.Max(maxSuffix, 0);
                    continue;
                }

                var parts = name.Substring(baseName.Length).TrimStart();
                if (parts.StartsWith("- ", StringComparison.Ordinal))
                {
                    var numberPart = parts.Substring(2);
                    if (int.TryParse(numberPart, out var n))
                    {
                        if (n > maxSuffix) maxSuffix = n;
                    }
                }
            }

            var newName = maxSuffix == 0 ? $"{baseName} - 1" : $"{baseName} - {maxSuffix + 1}";

            var newCategory = new Category
            {
                Name = newName,
                Order = Categories.Count
            };
            var newVm = new CategoryViewModel(newCategory);
            AttachCategoryHandlers(newVm);
            Categories.Add(newVm);
            return newVm;
        }

        public void AddRecipeIngredientsToCategory(Recipe recipe, Guid categoryId)
        {
            var cat = Categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            foreach (var ing in recipe.Ingredients)
            {
                var newP = new Product
                {
                    Name = ing.Name,
                    Unit = ing.Unit,
                    Quantity = ing.Quantity,
                    Store = ing.Store,
                    IsOptional = ing.IsOptional,
                    IsBought = false,
                    CategoryId = categoryId
                };

                var pvm = new ProductViewModel(newP);
                pvm.OnDelete += (s, e) => cat.Products.Remove(pvm);
                pvm.OnBoughtChanged += (s, e) => cat.MoveBoughtToEnd(pvm);
                cat.Products.Add(pvm);
            }
        }
    }
}
