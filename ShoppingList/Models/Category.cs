using System;
using System.Collections.Generic;

namespace ShoppingList.Models
{
    public class Category
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int Order { get; set; } = 0;
    }
}
