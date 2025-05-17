using Microsoft.Maui.Controls;
using CommunityToolkit.Maui;

namespace BarcodeProject.App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            this.UseMauiCommunityToolkit();
            MainPage = new NavigationPage(new MainPage());
        }
    }
}