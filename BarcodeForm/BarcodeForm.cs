using System;
using System.Drawing;
using System.Windows.Forms;
using BarcodeProject.Core;

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


        private void ProcessBarcode(string imagePath)
        {
            var image = new BarcodeImage(imagePath);

            // Шаг 1: Выделяем штрих-код и отображаем его
            Bitmap highlightedImage = image.HighlightBarcode();
            pictureBox.Image = highlightedImage;

            // Шаг 2: Получаем область штрих-кода
            Rectangle barcodeRegion = image.DetectBarcodeRegion();
            if (barcodeRegion == Rectangle.Empty)
            {
                txtResult.Text = "Штрих-код не найден.";
                return;
            }

            // Шаг 3: Получаем профиль только из области штрих-кода
            var profile = image.GetProfileFromRegion(barcodeRegion);

            // Шаг 4: Декодируем штрих-код
            string result = string.Empty;
            if (cmbBarcodeType.SelectedItem == null)
            {
                result = "Выберите тип штрих-кода.";
            }
            else
            {
                switch (cmbBarcodeType.SelectedItem.ToString())
                {
                    case "EAN-13":
                        result = EanUpcDecoder.DecodeEan13(profile);
                        break;
                    case "UPC-A":
                        result = EanUpcDecoder.DecodeUpcA(profile);
                        break;
                    case "Code 128":
                        result = Code128Decoder.Decode(profile);
                        break;
                    default:
                        result = "Выберите тип штрих-кода.";
                        break;
                }
            }

            txtResult.Text = result;
        }

        // Обработчик для кнопки "Распознать" (выделить штрих-код)
        //private void btnHighlightBarcode_Click(object sender, EventArgs e)
        //{
        //    if (_barcodeImage == null)
        //    {
        //        txtResult.Text = "Сначала откройте изображение.";
        //        return;
        //    }

        //    // Выделяем штрих-код и отображаем его
        //    _barcodeRegion = _barcodeImage.DetectBarcodeRegion();
        //    if (_barcodeRegion == Rectangle.Empty)
        //    {
        //        txtResult.Text = "Штрих-код не найден.";
        //        pictureBox.Image = new Bitmap(_originalImage); // Возвращаем оригинальное изображение
        //        return;
        //    }

        //    Bitmap highlightedImage = _barcodeImage.HighlightBarcode();
        //    pictureBox.Image = highlightedImage;
        //    txtResult.Text = "Штрих-код выделен.";
        //}
        private void btnHighlightBarcode_Click(object sender, EventArgs e)
        {
            if (_barcodeImage == null)
            {
                txtResult.Text = "Сначала откройте изображение.";
                return;
            }

            // Выделяем штрих-код
            _barcodeRegion = _barcodeImage.DetectBarcodeRegion();

            Bitmap highlightedImage;

            if (_barcodeRegion == Rectangle.Empty)
            {
                txtResult.Text = "Штрих-код не найден.";
                highlightedImage = new Bitmap(_originalImage); // Используем оригинальное изображение
            }
            else
            {
                txtResult.Text = "Штрих-код выделен.";
                highlightedImage = _barcodeImage.HighlightBarcode(); // Подсвечиваем найденный штрих-код
            }

            pictureBox.Image = highlightedImage;

            // Сохраняем все этапы обработки
            try
            {
               
                highlightedImage.Save("5_highlighted.png");

                txtResult.AppendText("\r\nИзображения сохранены рядом с .exe файлом.");
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

            // Получаем профиль из региона штрих-кода
            int[] profile = _barcodeImage.GetProfileFromRegion(_barcodeRegion);

            // Декодируем штрих-код
            string result = string.Empty;
            if (cmbBarcodeType.SelectedItem == null)
            {
                result = "Выберите тип штрих-кода.";
            }
            else
            {
                result = BarcodeAnalyzer.AnalyzeBarcode(profile, cmbBarcodeType.SelectedItem.ToString());
            }

            txtResult.Text = result;
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