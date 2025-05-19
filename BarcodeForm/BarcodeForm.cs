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

            if (cmbBarcodeType.SelectedItem == null)
            {
                rtbResult.Clear();
                rtbResult.Text = "Выберите тип штрих-кода.";
                return;
            }

            try
            {
                int[] profile = _barcodeImage.GetProfileFromRegion(_barcodeRegion);
                string result = BarcodeAnalyzer.AnalyzeBarcode(profile, cmbBarcodeType.SelectedItem.ToString());
                rtbResult.Clear();
                rtbResult.Text = result;

                var binary = Utils.Binarize(profile);
                var rle = Utils.RLE(binary);
                var (bitString, isValid) = Utils.ConvertToBitString(rle);
                try
                {
                    File.WriteAllText("binary_profile.txt", bitString);
                    File.WriteAllText("rle_debug.txt", string.Join(", ", rle.Select(item => $"({item.value}, {item.length})")));
                    rtbResult.AppendText($"\r\nБинарный профиль сохранён в binary_profile.txt");
                    rtbResult.AppendText($"\r\nRLE сохранён в rle_debug.txt");
                    rtbResult.AppendText($"\r\nДлина профиля: {profile.Length}");
                    rtbResult.AppendText($"\r\nДлина битовой строки: {bitString.Length}");
                    rtbResult.AppendText($"\r\nПервые 20 символов битовой строки: {bitString.Substring(0, Math.Min(20, bitString.Length))}");
                }
                catch (Exception ex)
                {
                    rtbResult.AppendText($"\r\nОшибка при сохранении отладочных файлов: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                rtbResult.Clear();
                rtbResult.Text = $"Ошибка при декодировании: {ex.Message}";
            }
        }

        
    }
}