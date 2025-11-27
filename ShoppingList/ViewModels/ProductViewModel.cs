using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ShoppingList.Models;

namespace ShoppingList.ViewModels
{
    public class ProductViewModel : BaseViewModel
    {
        private Product _model;
        public Product Model => _model;

        public Guid Id => _model.Id;
        public string Name { get => _model.Name; set { _model.Name = value; OnPropertyChanged(); } }
        public string Unit { get => _model.Unit; set { _model.Unit = value; OnPropertyChanged(); } }
        public double Quantity { get => _model.Quantity; set { _model.Quantity = value; OnPropertyChanged(); } }
        public bool IsBought { get => _model.IsBought; set { _model.IsBought = value; OnPropertyChanged(); OnBoughtChanged?.Invoke(this, EventArgs.Empty); } }
        public bool IsOptional { get => _model.IsOptional; set { _model.IsOptional = value; OnPropertyChanged(); } }
        public string Store { get => _model.Store; set { _model.Store = value; OnPropertyChanged(); } }
        public Guid CategoryId { get => _model.CategoryId; set { _model.CategoryId = value; OnPropertyChanged(); } }

        public event EventHandler? OnDelete;
        public event EventHandler? OnBoughtChanged;

        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand DeleteCommand { get; }

        public ProductViewModel(Product p)
        {
            _model = p;
            IncrementCommand = new Command(() => Quantity = Math.Round(Quantity + 1, 2));
            DecrementCommand = new Command(() => { if (Quantity > 0) Quantity = Math.Round(Math.Max(0, Quantity - 1),2); });
            DeleteCommand = new Command(() => OnDelete?.Invoke(this, EventArgs.Empty));
        }
    }
}
