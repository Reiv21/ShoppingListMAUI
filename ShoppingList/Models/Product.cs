using System;
using System.Text.Json.Serialization;

namespace ShoppingList.Models
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "szt.";
        public double Quantity { get; set; } = 1;
        public bool IsBought { get; set; } = false;
        public bool IsOptional { get; set; } = false;
        public string Store { get; set; } = ""; // nazwa sklepu
        public Guid CategoryId { get; set; }
    }
}
