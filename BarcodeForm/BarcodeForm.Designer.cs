﻿namespace BarcodeDecoderWinForms
{
    partial class BarcodeForm
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BarcodeForm));
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.файлToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnLoadImage = new System.Windows.Forms.ToolStripMenuItem();
            this.опцииToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnHighlightBarcode = new System.Windows.Forms.ToolStripMenuItem();
            this.btnDecodeBarcode = new System.Windows.Forms.ToolStripMenuItem();
            this.lblResult = new System.Windows.Forms.Label();
            this.cmbBarcodeType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.fileSystemWatcher1 = new System.IO.FileSystemWatcher();
            this.rtbResult = new System.Windows.Forms.RichTextBox();
            this.compressedPictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.compressedPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox
            // 
            this.pictureBox.Location = new System.Drawing.Point(12, 39);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(847, 645);
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.файлToolStripMenuItem,
            this.опцииToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1194, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // файлToolStripMenuItem
            // 
            this.файлToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnLoadImage});
            this.файлToolStripMenuItem.Name = "файлToolStripMenuItem";
            this.файлToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.файлToolStripMenuItem.Text = "Файл";
            // 
            // btnLoadImage
            // 
            this.btnLoadImage.Name = "btnLoadImage";
            this.btnLoadImage.Size = new System.Drawing.Size(198, 22);
            this.btnLoadImage.Text = "Открыть изображение";
            this.btnLoadImage.Click += new System.EventHandler(this.btnLoadImage_Click);
            // 
            // опцииToolStripMenuItem
            // 
            this.опцииToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnHighlightBarcode,
            this.btnDecodeBarcode});
            this.опцииToolStripMenuItem.Name = "опцииToolStripMenuItem";
            this.опцииToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.опцииToolStripMenuItem.Text = "Опции";
            // 
            // btnHighlightBarcode
            // 
            this.btnHighlightBarcode.Name = "btnHighlightBarcode";
            this.btnHighlightBarcode.Size = new System.Drawing.Size(180, 22);
            this.btnHighlightBarcode.Text = "Распознать";
            this.btnHighlightBarcode.Click += new System.EventHandler(this.btnHighlightBarcode_Click);
            // 
            // btnDecodeBarcode
            // 
            this.btnDecodeBarcode.Name = "btnDecodeBarcode";
            this.btnDecodeBarcode.Size = new System.Drawing.Size(180, 22);
            this.btnDecodeBarcode.Text = "Расшифровать";
            this.btnDecodeBarcode.Click += new System.EventHandler(this.btnDecodeBarcode_Click);
            // 
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.Location = new System.Drawing.Point(878, 93);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(62, 13);
            this.lblResult.TabIndex = 2;
            this.lblResult.Text = "Результат:";
            // 
            // cmbBarcodeType
            // 
            this.cmbBarcodeType.FormattingEnabled = true;
            this.cmbBarcodeType.Items.AddRange(new object[] {
            "EAN-13",
            "Code 128"});
            this.cmbBarcodeType.Location = new System.Drawing.Point(881, 53);
            this.cmbBarcodeType.Name = "cmbBarcodeType";
            this.cmbBarcodeType.Size = new System.Drawing.Size(291, 21);
            this.cmbBarcodeType.TabIndex = 5;
            this.cmbBarcodeType.Text = "EAN-13";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(878, 37);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Тип штрих-кода:";
            // 
            // fileSystemWatcher1
            // 
            this.fileSystemWatcher1.EnableRaisingEvents = true;
            this.fileSystemWatcher1.SynchronizingObject = this;
            // 
            // rtbResult
            // 
            this.rtbResult.Location = new System.Drawing.Point(881, 109);
            this.rtbResult.Name = "rtbResult";
            this.rtbResult.Size = new System.Drawing.Size(291, 260);
            this.rtbResult.TabIndex = 10;
            this.rtbResult.Text = "";
            // 
            // compressedPictureBox
            // 
            this.compressedPictureBox.Location = new System.Drawing.Point(881, 375);
            this.compressedPictureBox.Name = "compressedPictureBox";
            this.compressedPictureBox.Size = new System.Drawing.Size(291, 315);
            this.compressedPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.compressedPictureBox.TabIndex = 11;
            this.compressedPictureBox.TabStop = false;
            // 
            // BarcodeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1194, 702);
            this.Controls.Add(this.compressedPictureBox);
            this.Controls.Add(this.rtbResult);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmbBarcodeType);
            this.Controls.Add(this.lblResult);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "BarcodeForm";
            this.Text = "Сканер штрих-кодов";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.compressedPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem файлToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem btnLoadImage;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.ComboBox cmbBarcodeType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolStripMenuItem опцииToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem btnHighlightBarcode;
        private System.Windows.Forms.ToolStripMenuItem btnDecodeBarcode;
        private System.IO.FileSystemWatcher fileSystemWatcher1;
        private System.Windows.Forms.RichTextBox rtbResult;
        private System.Windows.Forms.PictureBox compressedPictureBox;
    }
}

