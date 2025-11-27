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
                var json = await File.ReadAllTextAsync(dataFile);
                return JsonSerializer.Deserialize<AppData>(json) ?? CreateDefaultData();
            }
            catch
            {
                return CreateDefaultData();
            }
        }

        public async Task SaveAsync(AppData data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dataFile, json);
        }

        public async Task ExportAsync(string path, AppData data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<AppData> ImportAsync(string path)
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<AppData>(json) ?? CreateDefaultData();
        }

        private AppData CreateDefaultData()
        {
            var data = new AppData();

            // predefiniowane kategorie
            var dairy = new Category { Name = "Nabiał", Order = 0 };
            var veg = new Category { Name = "Warzywa", Order = 1 };
            var electronics = new Category { Name = "Elektronika", Order = 2 };
            data.Categories.AddRange(new[] { dairy, veg, electronics });

            // przykładowe produkty
            data.Products.Add(new Product { Name = "Mleko", Unit = "l", Quantity = 1, CategoryId = dairy.Id });
            data.Products.Add(new Product { Name = "Jabłka", Unit = "kg", Quantity = 1.5, CategoryId = veg.Id });

            // dwa predefiniowane przepisy
            var r1 = new Recipe { Title = "Sałatka owocowa", Description = "Prosty przepis" };
            r1.Ingredients.Add(new Product { Name = "Jabłka", Unit = "szt.", Quantity = 3 });
            r1.Ingredients.Add(new Product { Name = "Banany", Unit = "szt.", Quantity = 2 });

            var r2 = new Recipe { Title = "Omlet", Description = "Szybki omlet" };
            r2.Ingredients.Add(new Product { Name = "Jajka", Unit = "szt.", Quantity = 3 });
            r2.Ingredients.Add(new Product { Name = "Mleko", Unit = "l", Quantity = 0.1 });

            data.Recipes.Add(r1);
            data.Recipes.Add(r2);

            return data;
        }
    }
}
