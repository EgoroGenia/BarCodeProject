using System;
using System.Drawing;
using System.Windows.Forms;
using BarcodeProject.Core;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing.Imaging;

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
                    compressedPictureBox.Image = null; // Очищаем сжатое изображение
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
                UpdateCompressedPictureBox();
                //rtbResult.AppendText("\r\nИзображения сохранены: 1_grayscale.png, 1a_clahe.png, 2_blurred.png, 3_gradient.png, 4_morphological.png, 5_threshold.png, 5a_contours.png, 6_highlighted.png");
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
        private void UpdateCompressedPictureBox()
        {
            if (_barcodeImage == null || _originalImage == null)
            {
                rtbResult.AppendText("\r\nСначала загрузите изображение.");
                compressedPictureBox.Image = null; // Очищаем compressedPictureBox
                return;
            }

            // Получаем максимальные размеры compressedPictureBox
            int maxWidth = compressedPictureBox.Width;
            int maxHeight = compressedPictureBox.Height;

            // Рассчитываем пропорции исходного изображения
            float aspectRatio = (float)_originalImage.Width / _originalImage.Height;

            // Рассчитываем новые размеры, чтобы изображение вписывалось в compressedPictureBox
            int targetWidth, targetHeight;
            if (aspectRatio > (float)maxWidth / maxHeight)
            {
                // Если изображение шире, ограничиваем по ширине
                targetWidth = maxWidth;
                targetHeight = (int)(targetWidth / aspectRatio);
            }
            else
            {
                // Если изображение выше, ограничиваем по высоте
                targetHeight = maxHeight;
                targetWidth = (int)(targetHeight * aspectRatio);
            }

            // Проверяем, чтобы размеры были положительными
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                rtbResult.AppendText("\r\nОшибка: размеры сжатого изображения недопустимы.");
                compressedPictureBox.Image = null;
                return;
            }

            // Создаём сжатое изображение
            using (Bitmap resizedImage = new Bitmap(targetWidth, targetHeight))
            {
                // Используем Graphics для высококачественного масштабирования
                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(_originalImage, 0, 0, targetWidth, targetHeight);
                }

                // Создаём копию для нанесения выделения
                Bitmap highlightedResized = new Bitmap(resizedImage);
                if (_barcodeRegion != Rectangle.Empty)
                {
                    // Масштабируем регион штрих-кода
                    float scaleX = (float)targetWidth / _originalImage.Width;
                    float scaleY = (float)targetHeight / _originalImage.Height;
                    Rectangle scaledRegion = new Rectangle(
                        (int)(_barcodeRegion.X * scaleX),
                        (int)(_barcodeRegion.Y * scaleY),
                        (int)(_barcodeRegion.Width * scaleX),
                        (int)(_barcodeRegion.Height * scaleY));

                    // Рисуем выделение
                    using (Graphics g = Graphics.FromImage(highlightedResized))
                    using (Pen pen = new Pen(Color.Red, 1)) // Тонкая линия для сжатого изображения
                    {
                        g.DrawRectangle(pen, scaledRegion);
                    }
                }

                // Устанавливаем изображение в compressedPictureBox
                compressedPictureBox.Image = new Bitmap(highlightedResized);

                // Сохраняем сжатое изображение
                try
                {
                    highlightedResized.Save("6_compressed_highlighted.png", ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    rtbResult.AppendText($"\r\nОшибка при сохранении сжатого изображения: {ex.Message}");
                }
            }
        }
    }
}