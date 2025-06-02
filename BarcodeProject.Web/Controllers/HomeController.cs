using BarcodeProject.Core;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace BarcodeProject.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Scan(IFormFile image, string barcodeType)
        {
            try
            {
                if (image == null || image.Length == 0)
                {
                    ViewData["Error"] = "Изображение не загружено.";
                    return View();
                }
                if (string.IsNullOrEmpty(barcodeType) || (barcodeType != "EAN-13" && barcodeType != "Code 128"))
                {
                    ViewData["Error"] = "Неверный тип штрихкода. Поддерживаются: EAN-13, Code 128.";
                    return View();
                }

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                using var bitmap = new Bitmap(memoryStream);

                var barcodeImage = new BarcodeImage(bitmap);
                var region = barcodeImage.DetectBarcodeRegion();

                string result;
                if (region.IsEmpty)
                {
                    result = "Штрихкод не обнаружен.";
                    ViewData["Result"] = result;
                    ViewData["HighlightedImage"] = null;
                }
                else
                {
                    var profile = barcodeImage.GetProfileFromRegion(region);
                    result = BarcodeAnalyzer.AnalyzeBarcode(profile, barcodeType);
                    using var highlightedImage = barcodeImage.HighlightBarcode();
                    var base64Image = BarcodeImage.BitmapToBase64(highlightedImage);
                    ViewData["Result"] = result;
                    ViewData["HighlightedImage"] = $"data:image/png;base64,{base64Image}";
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewData["Error"] = $"Ошибка обработки: {ex.Message}";
                return View();
            }
        }
    }
}