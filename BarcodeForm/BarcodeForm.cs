using System;
using System.Drawing;
using System.Windows.Forms;
using BarcodeProject.Core;
using System.IO;
using System.Linq;

namespace BarcodeDecoderWinForms
{
    public partial class BarcodeForm : Form
    {
        private Bitmap _originalImage;// Храним оригинальное изображение
        private BarcodeImage _barcodeImage; // Объект для обработки штрих-кода
        private Rectangle _barcodeRegion; // Храним регион штрих-кода

        public BarcodeForm()
        {
            InitializeComponent();
        }

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Изображения|*.bmp;*.jpg;*.png;*.jpeg;*.gif";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Загружаем оригинальное изображение и отображаем его
                    _originalImage = new Bitmap(openFileDialog.FileName);
                    pictureBox.Image = new Bitmap(_originalImage); // Создаем копию для отображения
                    txtResult.Text = "Изображение загружено.";
                    _barcodeImage = new BarcodeImage(openFileDialog.FileName);
                    _barcodeRegion = Rectangle.Empty; // Сбрасываем регион
                }
            }
        }


        private void btnHighlightBarcode_Click(object sender, EventArgs e)
        {
            if (_barcodeImage == null)
            {
                txtResult.Text = "Сначала откройте изображение.";
                return;
            }

            _barcodeRegion = _barcodeImage.DetectBarcodeRegion();
            Bitmap highlightedImage;

            if (_barcodeRegion == Rectangle.Empty)
            {
                txtResult.Text = "Штрих-код не найден.";
                highlightedImage = new Bitmap(_originalImage);
            }
            else
            {
                txtResult.Text = "Штрих-код выделен.";
                highlightedImage = _barcodeImage.HighlightBarcode();
            }

            pictureBox.Image = highlightedImage;

            try
            {
                txtResult.AppendText("\r\nИзображения сохранены: 1_grayscale.png, 1a_clahe.png, 2_blurred.png, 3_gradient.png, 4_morphological.png, 5_threshold.png, 5a_contours.png, 6_highlighted.png");
            }
            catch (Exception ex)
            {
                txtResult.AppendText($"\r\nОшибка при сохранении: {ex.Message}");
            }
        }

        // Обработчик для кнопки "Расшифровать"
        private void btnDecodeBarcode_Click(object sender, EventArgs e)
        {
            if (_barcodeImage == null)
            {
                txtResult.Text = "Сначала откройте изображение.";
                return;
            }

            if (_barcodeRegion == Rectangle.Empty)
            {
                txtResult.Text = "Сначала выделите штрих-код.";
                return;
            }

            if (cmbBarcodeType.SelectedItem == null)
            {
                txtResult.Text = "Выберите тип штрих-кода.";
                return;
            }

            try
            {
                // Получаем профиль
                int[] profile = _barcodeImage.GetProfileFromRegion(_barcodeRegion);

                // Декодируем штрих-код
                string result = BarcodeAnalyzer.AnalyzeBarcode(profile, cmbBarcodeType.SelectedItem.ToString());
                txtResult.Text = result;

                // Отладка
                var binary = Utils.Binarize(profile);
                var rle = Utils.RLE(binary);
                var (bitString, isValid) = Utils.ConvertToBitString(rle);
                try
                {
                    File.WriteAllText("binary_profile.txt", bitString);
                    File.WriteAllText("rle_debug.txt", string.Join(", ", rle.Select(item => $"({item.value}, {item.length})")));
                    txtResult.AppendText($"\r\nБинарный профиль сохранён в binary_profile.txt");
                    txtResult.AppendText($"\r\nRLE сохранён в rle_debug.txt");
                    txtResult.AppendText($"\r\nДлина профиля: {profile.Length}");
                    txtResult.AppendText($"\r\nДлина битовой строки: {bitString.Length}");
                    txtResult.AppendText($"\r\nПервые 20 символов битовой строки: {bitString.Substring(0, Math.Min(20, bitString.Length))}");
                }
                catch (Exception ex)
                {
                    txtResult.AppendText($"\r\nОшибка при сохранении отладочных файлов: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = $"Ошибка при декодировании: {ex.Message}";
            }
        }

        private void сБиблиотекамиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Изображения|*.bmp;*.jpg;*.png;*.jpeg;*.gif";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ProcessBarcodeLib(openFileDialog.FileName);
                }
            }
        }
        private void ProcessBarcodeLib(string imagePath)
        {
            var image = new BarcodeImage(imagePath);

            // Шаг 1: Выделяем штрих-код и отображаем его
            Bitmap highlightedImage = image.HighlightBarcode();
            pictureBox.Image = highlightedImage;

            // Шаг 2: Извлекаем профиль яркости по региону штрих-кода
            Rectangle region = image.DetectBarcodeRegion();
            if (region != Rectangle.Empty)
            {
                int[] profile = image.GetProfileFromRegion(region);

                // Шаг 3: Отобразим бинарный профиль в виде строки
                string binaryString = string.Join("", profile);
                txtResult.Text = binaryString;

                // Можно также реализовать парсинг под конкретные форматы (например, EAN-13)
            }
            else
            {
                txtResult.Text = "Штрих-код не найден.";
            }
        }
    }
}