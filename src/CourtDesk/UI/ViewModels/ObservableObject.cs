using System;
using System.ComponentModel;
using System.Windows.Input;

namespace CourtDesk.UI.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise(string name)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null)
            {
                h(this, new PropertyChangedEventArgs(name));
            }
        }

        protected bool Set<T>(ref T field, T value, string name)
        {
            if (Equals(field, value))
            {
                return false;
            }
            field = value;
            Raise(name);
            return true;
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _run;
        private readonly Func<object, bool> _can;

        public RelayCommand(Action<object> run) : this(run, null)
        {
        }

        public RelayCommand(Action<object> run, Func<object, bool> can)
        {
            if (run == null)
            {
                throw new ArgumentNullException("run");
            }
            _run = run;
            _can = can;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _can == null || _can(parameter);
        }

        public void Execute(object parameter)
        {
            _run(parameter);
        }

        public void RaiseCanExecute()
        {
            EventHandler h = CanExecuteChanged;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }
    }
}
