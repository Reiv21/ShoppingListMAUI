using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ShoppingList.Models;
using System.Linq;
using Microsoft.Maui.Storage;

namespace ShoppingList.Services
{
    public class AppData
    {
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
    }

    public class DataService
    {
        private readonly string dataFile = Path.Combine(FileSystem.AppDataDirectory, "shoppingdata.json");

        public async Task<AppData> LoadAsync()
        {
            try
            {
                if (!File.Exists(dataFile)) return CreateDefaultData();
                string json = await File.ReadAllTextAsync(dataFile);
                return JsonSerializer.Deserialize<AppData>(json) ?? CreateDefaultData();
            }
            catch
            {
                return CreateDefaultData();
            }
        }

        public async Task SaveAsync(AppData data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dataFile, json);
        }

        public async Task ExportAsync(string path, AppData data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<AppData> ImportAsync(string path)
        {
            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<AppData>(json) ?? CreateDefaultData();
        }

        private AppData CreateDefaultData()
        {
            AppData data = new AppData();

            // predefiniowane kategorie
            Category dairy = new Category { Name = "Nabiał", Order = 0 };
            Category veg = new Category { Name = "Warzywa", Order = 1 };
            Category electronics = new Category { Name = "Elektronika", Order = 2 };
            data.Categories.AddRange(new[] { dairy, veg, electronics });

            // przykladowe produkty
            data.Products.Add(new Product { Name = "Mleko", Unit = "l", Quantity = 1, CategoryId = dairy.Id });
            data.Products.Add(new Product { Name = "Jabłka", Unit = "kg", Quantity = 1.5, CategoryId = veg.Id });

            // dwa predefiniowane przepisy
            Recipe r1 = new Recipe { Title = "Sałatka owocowa", Description = "Prosty przepis" };
            r1.Ingredients.Add(new Product { Name = "Jabłka", Unit = "szt.", Quantity = 3 });
            r1.Ingredients.Add(new Product { Name = "Banany", Unit = "szt.", Quantity = 2 });

            Recipe r2 = new Recipe { Title = "Omlet", Description = "Szybki omlet" };
            r2.Ingredients.Add(new Product { Name = "Jajka", Unit = "szt.", Quantity = 3 });
            r2.Ingredients.Add(new Product { Name = "Mleko", Unit = "l", Quantity = 0.1 });

            data.Recipes.Add(r1);
            data.Recipes.Add(r2);

            return data;
        }

        public AppData Merge(AppData? currentData, AppData importedData, ImportMode mode)
        {
            currentData ??= CreateDefaultData();
            if (mode == ImportMode.Replace)
            {
                return importedData;
            }

            AppData merged = new AppData
            {
                Categories = currentData.Categories.Select(c => CloneCategory(c)).ToList(),
                Products = currentData.Products.Select(p => CloneProduct(p)).ToList(),
                Recipes = currentData.Recipes.Select(r => CloneRecipe(r)).ToList()
            };

            foreach (Category cat in importedData.Categories)
            {
                if (!merged.Categories.Any(c => string.Equals(c.Name, cat.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Category clone = CloneCategory(cat);
                    clone.Order = merged.Categories.Count;
                    merged.Categories.Add(clone);
                }
            }

            foreach (Product product in importedData.Products)
            {
                Category? category = merged.Categories.FirstOrDefault(c => c.Id == product.CategoryId);
                if (category == null)
                {
                    Category? originalCategory = importedData.Categories.FirstOrDefault(c => c.Id == product.CategoryId);
                    if (originalCategory != null)
                    {
                        Category? cloneCategory = merged.Categories.FirstOrDefault(c => string.Equals(c.Name, originalCategory.Name, StringComparison.OrdinalIgnoreCase));
                        if (cloneCategory == null)
                        {
                            cloneCategory = CloneCategory(originalCategory);
                            cloneCategory.Order = merged.Categories.Count;
                            merged.Categories.Add(cloneCategory);
                        }
                        category = cloneCategory;
                    }
                }

                Product? existing = merged.Products.FirstOrDefault(p => string.Equals(p.Name, product.Name, StringComparison.OrdinalIgnoreCase) && p.CategoryId == category?.Id);
                if (existing == null)
                {
                    Product clone = CloneProduct(product);
                    clone.CategoryId = category?.Id ?? clone.CategoryId;
                    merged.Products.Add(clone);
                }
                else
                {
                    existing.Quantity += product.Quantity;
                    existing.IsOptional |= product.IsOptional;
                    if (string.IsNullOrWhiteSpace(existing.Store) && !string.IsNullOrWhiteSpace(product.Store))
                    {
                        existing.Store = product.Store;
                    }
                }
            }

            foreach (Recipe recipe in importedData.Recipes)
            {
                if (!merged.Recipes.Any(r => string.Equals(r.Title, recipe.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    merged.Recipes.Add(CloneRecipe(recipe));
                }
            }

            return merged;
        }

        private static Category CloneCategory(Category source)
        {
            return new Category
            {
                Id = source.Id,
                Name = source.Name,
                Order = source.Order
            };
        }

        private static Product CloneProduct(Product source)
        {
            return new Product
            {
                Id = source.Id,
                Name = source.Name,
                Unit = source.Unit,
                Quantity = source.Quantity,
                IsBought = source.IsBought,
                IsOptional = source.IsOptional,
                Store = source.Store,
                CategoryId = source.CategoryId
            };
        }

        private static Recipe CloneRecipe(Recipe source)
        {
            Recipe clone = new Recipe
            {
                Id = source.Id,
                Title = source.Title,
                Description = source.Description,
                IsExpanded = source.IsExpanded
            };
            foreach (Product ingredient in source.Ingredients)
            {
                clone.Ingredients.Add(CloneProduct(ingredient));
            }
            return clone;
        }
    }
}
