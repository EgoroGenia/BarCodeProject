using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using BarcodeProject.Core;
using CommunityToolkit.Maui.Storage;

namespace BarcodeProject.App
{
    public partial class ScanOptionsPage : ContentPage
    {
        private readonly List<string> _barcodeTypes;
        private readonly Action<string> _updateResultCallback;

        public ScanOptionsPage(List<string> barcodeTypes, Action<string> updateResultCallback)
        {
            InitializeComponent();
            _barcodeTypes = barcodeTypes;
            _updateResultCallback = updateResultCallback;
        }

        private async void OnTakePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Error", "Camera permission denied.", "OK");
                    return;
                }

                var photo = await MediaPicker.CapturePhotoAsync();
                if (photo == null)
                    return;

                await ProcessImageAsync(photo.FullPath);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to capture photo: {ex.Message}", "OK");
            }
        }

        private async void OnChooseFromGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Error", "Storage permission denied.", "OK");
                    return;
                }

                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = FilePickerFileType.Images
                });

                if (fileResult == null)
                    return;

                await ProcessImageAsync(fileResult.FullPath);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to pick image: {ex.Message}", "OK");
            }
        }

        private async Task ProcessImageAsync(string filePath)
        {
            try
            {
                var barcodeImage = new BarcodeImage(filePath);
                var region = barcodeImage.DetectBarcodeRegion();
                if (region == Rectangle.Empty)
                {
                    _updateResultCallback("No barcode detected.");
                    await Navigation.PopModalAsync();
                    return;
                }

                var profile = barcodeImage.GetProfileFromRegion(region);
                string result = "No valid barcode found.";

                foreach (var type in _barcodeTypes)
                {
                    var decodeResult = BarcodeAnalyzer.AnalyzeBarcode(profile, type);
                    if (!decodeResult.StartsWith("Ошибка"))
                    {
                        result = $"Decoded {type}: {decodeResult}";
                        break;
                    }
                }

                _updateResultCallback(result);
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                _updateResultCallback($"Error processing image: {ex.Message}");
                await Navigation.PopModalAsync();
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}