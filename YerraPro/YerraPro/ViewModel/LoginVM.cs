using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using YerraPro.Views;

namespace YerraPro.ViewModel
{
    public class LoginVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Agent User
        {
            get => Helpers.user;
            set
            {
                Helpers.user = value;
            }
        }
        public string Password { get; set; }
        public LoginVM()
        {
            loginCommand = new RelayCommand(o => loginClick(o));

        }

        public string Name { get; set; }

        public ICommand loginCommand { get; set; }
        private void loginClick(object sender)
        {
            Helpers.mainFrame.Navigate(new RegisterPage());
        }

    }
}
