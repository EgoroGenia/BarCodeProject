using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Controls;
using BarcodeProject.Core;

namespace BarcodeProject.App
{
    public class BarcodeType
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        private ObservableCollection<BarcodeType> _barcodeTypes;

        public MainPage()
        {
            InitializeComponent();
            InitializeBarcodeTypes();
            BarcodeTypesCollection.ItemsSource = _barcodeTypes;
        }

        private void InitializeBarcodeTypes()
        {
            _barcodeTypes = new ObservableCollection<BarcodeType>
            {
                new BarcodeType { Name = "EAN-13", IsSelected = true },
                new BarcodeType { Name = "Code 128", IsSelected = false }
            };
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            var selectedTypes = _barcodeTypes.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            if (!selectedTypes.Any())
            {
                await DisplayAlert("Error", "Please select at least one barcode type.", "OK");
                return;
            }

            var scanOptionsPage = new ScanOptionsPage(selectedTypes, UpdateResult);
            await Navigation.PushModalAsync(scanOptionsPage);
        }

        private void UpdateResult(string result)
        {
            ResultLabel.Text = result;
        }
    }
}