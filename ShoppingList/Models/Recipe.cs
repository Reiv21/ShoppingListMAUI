using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShoppingList.Models
{
    public class Recipe : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _title = "";
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        private string _instructions = "";
        public string Instructions
        {
            get => _instructions;
            set
            {
                if (_instructions == value) return;
                _instructions = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Product> Ingredients { get; set; } = new ObservableCollection<Product>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
