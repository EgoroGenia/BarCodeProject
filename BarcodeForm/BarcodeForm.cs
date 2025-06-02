using System;
using System.Drawing;
using System.Windows.Forms;
using BarcodeProject.Core;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace BarcodeDecoderWinForms
{
    public partial class BarcodeForm : Form
    {
        private Bitmap _originalImage;
        private BarcodeImage _barcodeImage;
        private Rectangle _barcodeRegion;

        public BarcodeForm()
        {
            InitializeComponent();
            // Настройка RichTextBox
            rtbResult.Multiline = true;
            rtbResult.WordWrap = true;
            rtbResult.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbResult.ReadOnly = false; // Разрешаем редактирование
        }

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Изображения|*.bmp;*.jpg;*.png;*.jpeg;*.gif";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    _originalImage = new Bitmap(openFileDialog.FileName);
                    pictureBox.Image = new Bitmap(_originalImage);
                    rtbResult.Clear(); // Очищаем RichTextBox
                    rtbResult.Text = "Изображение загружено.";
                    _barcodeImage = new BarcodeImage(openFileDialog.FileName);
                    _barcodeRegion = Rectangle.Empty;
                }
            }
        }

        private void btnHighlightBarcode_Click(object sender, EventArgs e)
        {
            if (_barcodeImage == null)
            {
                rtbResult.Clear();
                rtbResult.Text = "Сначала откройте изображение.";
                return;
            }

            _barcodeRegion = _barcodeImage.DetectBarcodeRegion();
            Bitmap highlightedImage;

            if (_barcodeRegion == Rectangle.Empty)
            {
                rtbResult.Clear();
                rtbResult.Text = "Штрих-код не найден.";
                highlightedImage = new Bitmap(_originalImage);
            }
            else
            {
                rtbResult.Clear();
                rtbResult.Text = "Штрих-код выделен.";
                highlightedImage = _barcodeImage.HighlightBarcode();
            }

            pictureBox.Image = highlightedImage;

            try
            {
                rtbResult.AppendText("\r\nИзображения сохранены: 1_grayscale.png, 1a_clahe.png, 2_blurred.png, 3_gradient.png, 4_morphological.png, 5_threshold.png, 5a_contours.png, 6_highlighted.png");
            }
            catch (Exception ex)
            {
                rtbResult.AppendText($"\r\nОшибка при сохранении: {ex.Message}");
            }
        }

        private void btnDecodeBarcode_Click(object sender, EventArgs e)
        {
            if (_barcodeImage == null)
            {
                rtbResult.Clear();
                rtbResult.Text = "Сначала откройте изображение.";
                return;
            }

            if (_barcodeRegion == Rectangle.Empty)
            {
                rtbResult.Clear();
                rtbResult.Text = "Сначала выделите штрих-код.";
                return;
            }

            try
            {
                int[] profile = _barcodeImage.GetProfileFromRegion(_barcodeRegion);
                var binary = Utils.Binarize(profile);
                var rle = Utils.RLE(binary);
                var trimmedRle = Utils.TrimWhitespace(rle);
                var (bitString, isValid) = Utils.ConvertToBitString(rle, "EAN-13");
                string result = BarcodeAnalyzer.AnalyzeBarcode(profile, "EAN-13");

                try
                {
                    // Вычисляем moduleSize для отладки
                    double moduleSize = trimmedRle.Any() ? trimmedRle.Select(item => item.length).Where(l => l > 0).Median() : 0;
                    if (trimmedRle.Count >= 3) // Проверка для EAN-13
                    {
                        var startGuardRle = trimmedRle.Take(3).ToList();
                        double guardModuleSize = startGuardRle.Sum(item => item.length) / 3.0;
                        moduleSize = (moduleSize + guardModuleSize) / 2.0;
                    }

                    // Сохраняем отладочные данные
                    File.WriteAllText("binary_profile.txt", bitString);
                    File.WriteAllText("rle_debug.txt", string.Join(", ", rle.Select(item => $"({item.value}, {item.length})")));
                    File.WriteAllText("rle_trimmed.txt", trimmedRle.Any()
                        ? string.Join(", ", trimmedRle.Select(item => $"({item.value}, {item.length})"))
                        : "Обрезанный RLE пуст");
                    File.WriteAllText("debug_log.txt",
                        $"Длина профиля: {profile.Length}\r\n" +
                        $"Длина битовой строки: {bitString.Length}\r\n" +
                        $"Первые 20 символов битовой строки: {(bitString.Length > 0 ? bitString.Substring(0, Math.Min(20, bitString.Length)) : "Пустая строка")}\r\n" +
                        $"Валидность: {isValid}\r\n" +
                        $"ModuleSize: {moduleSize:F2}\r\n" +
                        $"Общее количество модулей: {bitString.Length}");

                    rtbResult.AppendText($"\r\nБинарный профиль сохранён в binary_profile.txt");
                    rtbResult.AppendText($"\r\nRLE сохранён в rle_debug.txt");
                    rtbResult.AppendText($"\r\nОбрезанный RLE сохранён в rle_trimmed.txt");
                    rtbResult.AppendText($"\r\nДлина профиля: {profile.Length}");
                    rtbResult.AppendText($"\r\nДлина битовой строки: {bitString.Length}");
                    rtbResult.AppendText($"\r\nПервые 20 символов битовой строки: {(bitString.Length > 0 ? bitString.Substring(0, Math.Min(20, bitString.Length)) : "Пустая строка")}");
                    rtbResult.AppendText($"\r\nВалидность: {isValid}");
                    rtbResult.AppendText($"\r\nModuleSize: {moduleSize:F2}");
                }
                catch (Exception ex)
                {
                    rtbResult.AppendText($"\r\nОшибка при сохранении отладочных файлов: {ex.Message}");
                }

                rtbResult.Clear();
                rtbResult.Text = $"Результат: {result}";
            }
            catch (Exception ex)
            {
                rtbResult.Clear();
                rtbResult.Text = $"Ошибка при декодировании: {ex.Message}";
            }
        }

        // Вспомогательный метод для вычисления moduleSize (для отладки)
        private static double CalculateModuleSize(List<(int value, int length)> rle, string barcodeType)
        {
            var lengths = rle.Select(item => item.length).Where(l => l > 0).ToList();
            if (lengths.Count == 0) return 0;

            double moduleSize = lengths.Median();
            if (barcodeType == "EAN-13" && rle.Count >= 3)
            {
                var startGuardRle = rle.Take(3).ToList();
                double guardModuleSize = startGuardRle.Sum(item => item.length) / 3.0;
                moduleSize = Math.Max(moduleSize, guardModuleSize);
            }
            else if (barcodeType == "Code128" && rle.Count >= 4)
            {
                var startGuardRle = rle.Take(4).ToList();
                double guardModuleSize = startGuardRle.Sum(item => item.length) / 4.0;
                moduleSize = Math.Max(moduleSize, guardModuleSize);
            }
            return moduleSize < 1 ? 1 : moduleSize;
        }

        // Вспомогательный метод для вычисления общего количества модулей (для отладки)
        private static int CalculateTotalModules(List<(int value, int length)> rle, string barcodeType)
        {
            double moduleSize = CalculateModuleSize(rle, barcodeType);
            int totalModules = 0;
            foreach (var item in rle)
            {
                double normalized = item.length / moduleSize;
                int modules = (int)Math.Round(normalized);
                if (modules < 1) modules = 1;
                totalModules += modules;
            }
            return totalModules;
        }

    }
}