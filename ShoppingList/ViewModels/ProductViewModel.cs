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
        public string Name
        {
            get => _model.Name;
            set
            {
                if (_model.Name == value) return;
                _model.Name = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public string Unit
        {
            get => _model.Unit;
            set
            {
                if (_model.Unit == value) return;
                _model.Unit = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public double Quantity
        {
            get => _model.Quantity;
            set
            {
                if (Math.Abs(_model.Quantity - value) < 0.0001) return;
                _model.Quantity = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsBought
        {
            get => _model.IsBought;
            set
            {
                if (_model.IsBought == value)
                {
                    return;
                }

                _model.IsBought = value;
                OnPropertyChanged();
                OnBoughtChanged?.Invoke(this, EventArgs.Empty);
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsOptional
        {
            get => _model.IsOptional;
            set
            {
                if (_model.IsOptional == value) return;
                _model.IsOptional = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public string Store
        {
            get => _model.Store;
            set
            {
                if (_model.Store == value) return;
                _model.Store = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public Guid CategoryId
        {
            get => _model.CategoryId;
            set
            {
                if (_model.CategoryId == value) return;
                _model.CategoryId = value;
                OnPropertyChanged();
                OnChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? OnDelete;
        public event EventHandler? OnBoughtChanged;
        public event EventHandler? OnChanged;

        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand DeleteCommand { get; }

        public ProductViewModel(Product p)
        {
            _model = p;
            IncrementCommand = new Command(() => Quantity = Math.Round(Quantity + 1, 2));
            DecrementCommand = new Command(() => { if (Quantity > 0) Quantity = Math.Round(Math.Max(0, Quantity - 1), 2); });
            DeleteCommand = new Command(() => OnDelete?.Invoke(this, EventArgs.Empty));
        }
    }
}
