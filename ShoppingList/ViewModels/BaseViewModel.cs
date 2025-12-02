using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShoppingList.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        // viewModel z ktorego kazdy inny dziedziczy
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName]string propName = "")
        {
            if (Equals(backingStore, value)) return false;
            backingStore = value;
            OnPropertyChanged(propName);
            return true;
        }
    }
}
