using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using YerraPro.Views;

namespace YerraPro.ViewModel
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainWindowVM()
        {
            PreNavigationCommand = new RelayCommand(o => preNavigationClick(o));
            CloseCommand = new RelayCommand(o => closeClick(o));
            this.MainFrame = new Frame();
            MainFrame.Navigate(new RegisterPage());
        }

        public Frame MainFrame
        {
            get => Helpers.mainFrame;
            set
            {
                Helpers.mainFrame = value;
                OnPropertyChanged("MainFrame");
            }
        }


        public ICommand PreNavigationCommand { get; set; }
        private void preNavigationClick(object sender)
        {
            this.MainFrame.Navigate(new RegisterPage());
        }

        
        public ICommand CloseCommand { get; set; }
        private void closeClick(object sender)
        {
            Environment.Exit(Environment.ExitCode);
        }

    }



    class Helpers
    {
        public static Frame mainFrame = new Frame();
        public static Agent user = new Agent();
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute(parameter ?? "<N/A>");
        }

    }

}
