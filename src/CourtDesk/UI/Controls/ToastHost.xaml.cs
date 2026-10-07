using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Controls
{
    public sealed class ToastItem : ObservableObject
    {
        private readonly ToastHost _host;
        private string _text;

        public ToastItem(ToastHost host, string text)
        {
            _host = host;
            _text = text;
            Dismiss = new RelayCommand(delegate(object o) { _host.Remove(this); });
        }

        public string Text
        {
            get { return _text; }
        }

        public ICommand Dismiss { get; private set; }
    }

    public partial class ToastHost : UserControl
    {
        private readonly ObservableCollection<ToastItem> _items = new ObservableCollection<ToastItem>();

        public ToastHost()
        {
            InitializeComponent();
            List.ItemsSource = _items;
        }

        public void Show(string text)
        {
            while (_items.Count >= 3)
            {
                _items.RemoveAt(0);
            }
            _items.Add(new ToastItem(this, text));
        }

        internal void Remove(ToastItem item)
        {
            _items.Remove(item);
        }

        public int Count
        {
            get { return _items.Count; }
        }
    }
}
