﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BarcodeProject.Core
{
    public class BarcodeImage
    {
        private Bitmap _image;
        private int[,] _grayImage;
        private Rectangle _barcodeRegion;

        public BarcodeImage(Bitmap image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _grayImage = ConvertToGrayscale(_image);
        }

        public BarcodeImage(string filePath)
        {
            using (Bitmap temp = new Bitmap(filePath))
            {
                _image = new Bitmap(temp);
            }
            _grayImage = ConvertToGrayscale(_image);
        }

        // Градации серого
        private int[,] ConvertToGrayscale(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int[,] gray = new int[width, height];

            // Блокируем данные изображения
            BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                IntPtr scan0 = bmpData.Scan0;
                byte[] pixels = new byte[stride * height];
                Marshal.Copy(scan0, pixels, 0, pixels.Length);

                Parallel.For(0, height, y =>
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int pixelOffset = rowOffset + x * 3; // 3 байта на пиксель (BGR)
                        byte b = pixels[pixelOffset];
                        byte g = pixels[pixelOffset + 1];
                        byte r = pixels[pixelOffset + 2];
                        int luminance = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                        gray[x, y] = luminance;
                    }
                });
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            SaveArrayAsImage(gray, "1_grayscale.png");
            return gray;
        }

        private int[,] EnhanceContrast(int[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] output = new int[width, height];

            int[] histogram = new int[256];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    histogram[input[x, y]]++;

            int[] cdf = new int[256];
            cdf[0] = histogram[0];
            for (int i = 1; i < 256; i++)
                cdf[i] = cdf[i - 1] + histogram[i];

            int cdfMin = cdf.FirstOrDefault(v => v > 0);
            int totalPixels = width * height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int value = input[x, y];
                    int newValue = (int)((cdf[value] - cdfMin) * 255f / (totalPixels - cdfMin));
                    output[x, y] = Math.Max(0, Math.Min(255, newValue));
                }
            }

            SaveArrayAsImage(output, "0_contrast_enhanced.png");
            return output;
        }

        private bool IsLowContrast(int[,] image)
        {
            int width = image.GetLength(0);
            int height = image.GetLength(1);

            double sum = 0, sumSq = 0;
            int count = 0;

            for (int y = 0; y < height; y += 5)
            {
                for (int x = 0; x < width; x += 5)
                {
                    int val = image[x, y];
                    sum += val;
                    sumSq += val * val;
                    count++;
                }
            }

            double variance = (sumSq - sum * sum / count) / count;
            double stdDev = Math.Sqrt(variance);

            return stdDev < 30;
        }

        private int[,] ApplyHighPassFilter(int[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] output = new int[width, height];

            int[,] kernel = {{-1, -1, -1},
                            {-1,  8, -1},
                            {-1, -1, -1}};

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int sum = 0;
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            sum += input[x + kx, y + ky] * kernel[ky + 1, kx + 1];
                        }
                    }
                    output[x, y] = Math.Max(0, Math.Min(255, sum + 128));
                }
            }

            SaveArrayAsImage(output, "0_highpass.png");
            return output;
        }

        private int[,] GaussianBlur(int[,] input, double sigma = 1.4)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] result = new int[width, height];

            int kernelSize = (int)(2 * Math.Ceiling(3 * sigma) + 1);
            int halfKernel = kernelSize / 2;
            float[,] kernel = new float[kernelSize, kernelSize];
            float kernelSum = 0;

            for (int ky = -halfKernel; ky <= halfKernel; ky++)
            {
                for (int kx = -halfKernel; kx <= halfKernel; kx++)
                {
                    float value = (float)(Math.Exp(-(kx * kx + ky * ky) / (2 * sigma * sigma)) / (2 * Math.PI * sigma * sigma));
                    kernel[ky + halfKernel, kx + halfKernel] = value;
                    kernelSum += value;
                }
            }

            for (int ky = 0; ky < kernelSize; ky++)
                for (int kx = 0; kx < kernelSize; kx++)
                    kernel[ky, kx] /= kernelSum;

            for (int y = halfKernel; y < height - halfKernel; y++)
            {
                for (int x = halfKernel; x < width - halfKernel; x++)
                {
                    float sum = 0;
                    for (int ky = -halfKernel; ky <= halfKernel; ky++)
                        for (int kx = -halfKernel; kx <= halfKernel; kx++)
                            sum += input[x + kx, y + ky] * kernel[ky + halfKernel, kx + halfKernel];
                    result[x, y] = (int)Math.Round(sum);
                    if (result[x, y] < 0) result[x, y] = 0;
                    if (result[x, y] > 255) result[x, y] = 255;
                }
            }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (y < halfKernel || y >= height - halfKernel || x < halfKernel || x >= width - halfKernel)
                        result[x, y] = input[x, y];

            SaveArrayAsImage(result, "2_blurred.png");
            return result;
        }

        private int[,] CannyEdgeDetection(int[,] input, double sigma = 1.4, int lowThreshold = 50, int highThreshold = 100)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] result = new int[width, height];

            int[,] blurred = GaussianBlur(input, sigma);

            double[,] gradX = new double[width, height];
            double[,] gradY = new double[width, height];
            double[,] magnitude = new double[width, height];
            double[,] direction = new double[width, height];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    gradX[x, y] = -blurred[x - 1, y - 1] - 2 * blurred[x - 1, y] - blurred[x - 1, y + 1] +
                                   blurred[x + 1, y - 1] + 2 * blurred[x + 1, y] + blurred[x + 1, y + 1];
                    gradY[x, y] = -blurred[x - 1, y - 1] - 2 * blurred[x, y - 1] - blurred[x + 1, y - 1] +
                                   blurred[x - 1, y + 1] + 2 * blurred[x, y + 1] + blurred[x + 1, y + 1];
                    magnitude[x, y] = Math.Sqrt(gradX[x, y] * gradX[x, y] + gradY[x, y] * gradY[x, y]);
                    direction[x, y] = Math.Atan2(gradY[x, y], gradX[x, y]) * 180 / Math.PI;
                    if (direction[x, y] < 0) direction[x, y] += 360;
                }
            }

            int[,] suppressed = new int[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double angle = direction[x, y];
                    double mag = magnitude[x, y];
                    int mag1 = 0, mag2 = 0;

                    if ((angle >= 0 && angle < 22.5) || (angle >= 157.5 && angle < 202.5) || (angle >= 337.5 && angle <= 360))
                    {
                        mag1 = (int)magnitude[x + 1, y];
                        mag2 = (int)magnitude[x - 1, y];
                    }
                    else if ((angle >= 22.5 && angle < 67.5) || (angle >= 202.5 && angle < 247.5))
                    {
                        mag1 = (int)magnitude[x + 1, y - 1];
                        mag2 = (int)magnitude[x - 1, y + 1];
                    }
                    else if ((angle >= 67.5 && angle < 112.5) || (angle >= 247.5 && angle < 292.5))
                    {
                        mag1 = (int)magnitude[x, y - 1];
                        mag2 = (int)magnitude[x, y + 1];
                    }
                    else if ((angle >= 112.5 && angle < 157.5) || (angle >= 292.5 && angle < 337.5))
                    {
                        mag1 = (int)magnitude[x - 1, y - 1];
                        mag2 = (int)magnitude[x + 1, y + 1];
                    }

                    if (mag >= mag1 && mag >= mag2)
                        suppressed[x, y] = (int)mag;
                    else
                        suppressed[x, y] = 0;
                }
            }

            int[,] thresholded = new int[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int mag = suppressed[x, y];
                    if (mag >= highThreshold)
                        thresholded[x, y] = 255;
                    else if (mag >= lowThreshold)
                        thresholded[x, y] = 128;
                    else
                        thresholded[x, y] = 0;
                }
            }

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (thresholded[x, y] == 128)
                    {
                        bool connected = false;
                        for (int dy = -1; dy <= 1 && !connected; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (thresholded[x + dx, y + dy] == 255)
                                {
                                    connected = true;
                                    break;
                                }
                        result[x, y] = connected ? 255 : 0;
                    }
                    else
                        result[x, y] = thresholded[x, y];
                }
            }

            SaveArrayAsImage(result, "3_canny.png");
            return result;
        }

        private int[,] MorphologicalClosing(int[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] dilated = new int[width, height];
            int[,] result = new int[width, height];

            int kw = Math.Max(15, width / 40);
            int kh = Math.Max(5, height / 80);

            for (int y = kh / 2; y < height - kh / 2; y++)
            {
                for (int x = kw / 2; x < width - kw / 2; x++)
                {
                    int maxVal = 0;
                    for (int ky = -kh / 2; ky <= kh / 2; ky++)
                        for (int kx = -kw / 2; kx <= kw / 2; kx++)
                        {
                            int val = input[x + kx, y + ky];
                            if (val > maxVal) maxVal = val;
                        }
                    dilated[x, y] = maxVal;
                }
            }

            for (int y = kh / 2; y < height - kh / 2; y++)
            {
                for (int x = kw / 2; x < width - kw / 2; x++)
                {
                    int minVal = 255;
                    for (int ky = -kh / 2; ky <= kh / 2; ky++)
                        for (int kx = -kw / 2; kx <= kw / 2; kx++)
                        {
                            int val = dilated[x + kx, y + ky];
                            if (val < minVal) minVal = val;
                        }
                    result[x, y] = minVal;
                }
            }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (y < kh / 2 || y >= height - kh / 2 || x < kw / 2 || x >= width - kw / 2)
                        result[x, y] = input[x, y];

            SaveArrayAsImage(result, "4_closed.png");
            return result;
        }

        private int ComputeOtsuThreshold(int[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[] histogram = new int[256];
            int totalPixels = width * height;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    histogram[input[x, y]]++;

            float sum = 0;
            for (int i = 0; i < 256; i++)
                sum += i * histogram[i];
            float sumB = 0;
            int wB = 0, wF = 0;
            float maxVariance = 0;
            int threshold = 0;

            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;
                wF = totalPixels - wB;
                if (wF == 0) break;
                sumB += t * histogram[t];
                float mB = sumB / wB;
                float mF = (sum - sumB) / wF;
                float variance = wB * wF * (mB - mF) * (mB - mF);
                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = t;
                }
            }

            return threshold;
        }
                
        private Rectangle[] FindContours(int[,] binary)
        {
            int width = binary.GetLength(0);
            int height = binary.GetLength(1);
            bool[,] visited = new bool[width, height];
            var rectangles = new List<Rectangle>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (binary[x, y] == 255 && !visited[x, y])
                    {
                        int minX = x, maxX = x, minY = y, maxY = y;
                        var stack = new Stack<Point>();
                        var contourPoints = new List<Point>();
                        stack.Push(new Point(x, y));
                        visited[x, y] = true;
                        int whitePixelCount = 1;

                        while (stack.Count > 0)
                        {
                            var p = stack.Pop();
                            contourPoints.Add(p);
                            if (p.X < minX) minX = p.X;
                            if (p.X > maxX) maxX = p.X;
                            if (p.Y < minY) minY = p.Y;
                            if (p.Y > maxY) maxY = p.Y;

                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int nx = p.X + dx;
                                    int ny = p.Y + dy;
                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                        binary[nx, ny] == 255 && !visited[nx, ny])
                                    {
                                        stack.Push(new Point(nx, ny));
                                        visited[nx, ny] = true;
                                        whitePixelCount++;
                                    }
                                }
                        }

                        int w = maxX - minX + 1;
                        int h = maxY - minY + 1;
                        int minWidth = width / 30; // Relaxed minimum width
                        int minHeight = height / 50; // Relaxed minimum height
                        float aspectRatio = w / (float)h;
                        double fillRatio = whitePixelCount / (double)(w * h);

                        // Stricter aspect ratio: between 1.5 and 5.0
                        // Lower fill ratio threshold to 0.2 for irregular shapes
                        if (w > minWidth && h > minHeight && aspectRatio > 1.1 && aspectRatio <10.0 && fillRatio > 0.2)
                        {
                           rectangles.Add(new Rectangle(minX, minY, w, h));
                        }
                    }
                }
            }

            var mergedRectangles = MergeCloseRectangles(rectangles, binary, width / 30, height / 30);
            SaveContoursImage(binary, mergedRectangles, "6_contours.png");
            return mergedRectangles.ToArray();
        }

        private List<Rectangle> MergeCloseRectangles(List<Rectangle> rectangles, int[,] binary, int maxDx, int maxDy)
        {
            var merged = new List<Rectangle>();
            bool[] used = new bool[rectangles.Count];

            for (int i = 0; i < rectangles.Count; i++)
            {
                if (used[i]) continue;

                Rectangle current = rectangles[i];
                int minX = current.X, maxX = current.X + current.Width - 1;
                int minY = current.Y, maxY = current.Y + current.Height - 1;
                int whitePixelCount = 0;

                for (int y = Math.Max(0, current.Y); y < Math.Min(binary.GetLength(1), current.Y + current.Height); y++)
                    for (int x = Math.Max(0, current.X); x < Math.Min(binary.GetLength(0), current.X + current.Width); x++)
                        if (binary[x, y] == 255)
                            whitePixelCount++;

                used[i] = true;

                for (int j = i + 1; j < rectangles.Count; j++)
                {
                    if (used[j]) continue;

                    Rectangle other = rectangles[j];
                    int dx = Math.Min(
                        Math.Abs(current.X - other.X),
                        Math.Abs((current.X + current.Width) - (other.X + other.Width)));
                    int dy = Math.Min(
                        Math.Abs(current.Y - other.Y),
                        Math.Abs((current.Y + current.Height) - (other.Y + other.Height)));

                    if (dx <= maxDx && dy <= maxDy)
                    {
                        minX = Math.Min(minX, other.X);
                        maxX = Math.Max(maxX, other.X + other.Width - 1);
                        minY = Math.Min(minY, other.Y);
                        maxY = Math.Max(maxY, other.Y + other.Height - 1);

                        for (int y = Math.Max(0, other.Y); y < Math.Min(binary.GetLength(1), other.Y + other.Height); y++)
                            for (int x = Math.Max(0, other.X); x < Math.Min(binary.GetLength(0), other.X + other.Width); x++)
                                if (binary[x, y] == 255)
                                    whitePixelCount++;

                        used[j] = true;
                    }
                }

                int w = maxX - minX + 1;
                int h = maxY - minY + 1;
                float aspectRatio = w / (float)h;
                double newFillRatio = whitePixelCount / (double)(w * h);

                // Apply stricter aspect ratio and fill ratio checks after merging
                if (aspectRatio > 1.1 && aspectRatio < 10.0 && newFillRatio > 0.2)
                    merged.Add(new Rectangle(minX, minY, w, h));
            }

            return merged;
        }
                
        private void SaveContoursImage(int[,] binary, List<Rectangle> rectangles, string filename)
        {
            int width = binary.GetLength(0);
            int height = binary.GetLength(1);
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int value = binary[x, y];
                        Color color = value == 255 ? Color.White : Color.Black;
                        bitmap.SetPixel(x, y, color);
                    }

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    int padding = 5;
                    foreach (var rect in rectangles)
                    {
                        int newX = Math.Max(0, rect.X - padding);
                        int newY = Math.Max(0, rect.Y - padding);
                        int newWidth = Math.Min(width - newX, rect.Width + 2 * padding);
                        int newHeight = Math.Min(height - newY, rect.Height + 2 * padding);
                        g.DrawRectangle(Pens.Red, newX, newY, newWidth - 1, newHeight - 1);
                    }
                }

                try
                {
                    bitmap.Save(filename, ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving {filename}: {ex.Message}");
                }
            }
        }

        private void SaveArrayAsImage(int[,] data, string filename)
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int value = data[x, y];
                        if (value < 0) value = 0;
                        if (value > 255) value = 255;
                        Color color = Color.FromArgb(value, value, value);
                        bmp.SetPixel(x, y, color);
                    }

                try
                {
                    bmp.Save(filename, ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving {filename}: {ex.Message}");
                }
            }
        }

        private int[,] FilterNoiseContours(int[,] edges)
        {
            int width = edges.GetLength(0);
            int height = edges.GetLength(1);
            int[,] filtered = new int[width, height];
            bool[,] visited = new bool[width, height];
            int minContourSize = Math.Max(10, Math.Min(width, height) / 50); // Minimum size for contours
            float minAspectRatio = 2.0f; // Minimum aspect ratio for barcode-like lines

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (edges[x, y] == 255 && !visited[x, y])
                    {
                        int minX = x, maxX = x, minY = y, maxY = y;
                        int pixelCount = 0;
                        var stack = new Stack<Point>();
                        stack.Push(new Point(x, y));
                        visited[x, y] = true;

                        while (stack.Count > 0)
                        {
                            var p = stack.Pop();
                            pixelCount++;
                            if (p.X < minX) minX = p.X;
                            if (p.X > maxX) maxX = p.X;
                            if (p.Y < minY) minY = p.Y;
                            if (p.Y > maxY) maxY = p.Y;

                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int nx = p.X + dx;
                                    int ny = p.Y + dy;
                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                        edges[nx, ny] == 255 && !visited[nx, ny])
                                    {
                                        stack.Push(new Point(nx, ny));
                                        visited[nx, ny] = true;
                                    }
                                }
                        }

                        int w = maxX - minX + 1;
                        int h = maxY - minY + 1;
                        float aspectRatio = w / (float)h;

                        // Keep contours that are large enough and have a barcode-like aspect ratio
                        bool isValid = pixelCount > minContourSize && (aspectRatio > minAspectRatio || 1.0f / aspectRatio > minAspectRatio);

                        if (isValid)
                        {
                            for (int ny = Math.Max(0, minY); ny <= Math.Min(height - 1, maxY); ny++)
                                for (int nx = Math.Max(0, minX); nx <= Math.Min(width - 1, maxX); nx++)
                                    if (edges[nx, ny] == 255 && visited[nx, ny])
                                        filtered[nx, ny] = 255;
                        }
                    }
                }
            }

            SaveArrayAsImage(filtered, "3.5_filtered_edges.png");
            return filtered;
        }

        public Rectangle DetectBarcodeRegion()
        {
            int[,] enhanced = EnhanceContrast(_grayImage);
            bool isLowContrast = IsLowContrast(_grayImage);

            int[,] edges = CannyEdgeDetection(isLowContrast ? enhanced : _grayImage, sigma: 1.5, lowThreshold: 50, highThreshold: 100);
            int[,] filteredEdges = FilterNoiseContours(edges); // Apply noise filtering
            int[,] closed = MorphologicalClosing(filteredEdges);
            var contours = FindContours(closed);

            if (contours.Length == 0)
            {
                if (isLowContrast)
                {
                    int[,] highPass = ApplyHighPassFilter(enhanced);
                    edges = CannyEdgeDetection(highPass, sigma: 1.4, lowThreshold: 50, highThreshold: 100);
                    filteredEdges = FilterNoiseContours(edges); // Apply noise filtering again
                    closed = MorphologicalClosing(filteredEdges);
                    contours = FindContours(closed);
                }
                _barcodeRegion = Rectangle.Empty;
            }
            else
            {
                _barcodeRegion = contours.OrderByDescending(r => r.Width * r.Height).First();
                _barcodeRegion = Rectangle.Intersect(_barcodeRegion, new Rectangle(0, 0, _grayImage.GetLength(0), _grayImage.GetLength(1)));
                if (_barcodeRegion.Width <= 0 || _barcodeRegion.Height <= 0)
                    _barcodeRegion = Rectangle.Empty;
            }

            return _barcodeRegion;
        }

        public Bitmap HighlightBarcode()
        {
            using (Bitmap result = new Bitmap(_image))
            {
                if (_barcodeRegion != Rectangle.Empty)
                {
                    using (Graphics g = Graphics.FromImage(result))
                    using (Pen pen = new Pen(Color.Red, 3))
                        g.DrawRectangle(pen, _barcodeRegion);
                }

                try
                {
                    result.Save("6_highlighted.png", ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving 6_highlighted.png: {ex.Message}");
                }

                return new Bitmap(result);
            }
        }
       
        public int[] GetProfileFromRegion(Rectangle region)
        {
            try
            {
                if (region.IsEmpty)
                    throw new ArgumentException("Region cannot be empty.");

                int width = region.Width;
                int height = region.Height;

                // Проверка минимальных размеров региона
                if (width < 10 || height < 10)
                    throw new ArgumentException("Region is too small to process.");

                // Шаг 1: Извлечение подрегиона
                int[,] regionImage = new int[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int imageX = region.X + x;
                        int imageY = region.Y + y;
                        if (imageX >= 0 && imageX < _grayImage.GetLength(0) && imageY >= 0 && imageY < _grayImage.GetLength(1))
                        {
                            regionImage[x, y] = _grayImage[imageX, imageY];
                        }
                        else
                        {
                            regionImage[x, y] = 255; // Белый фон для выходов за границы
                        }
                    }
                }
                SaveArrayAsImage(regionImage, "7.1_region_grayscale.png");

                // Шаг 2: Супер-масштабирование, если регион маленький
                if (width < 200 || height < 50)
                {
                    regionImage = SuperResolution(regionImage, 2);
                    width = regionImage.GetLength(0);
                    height = regionImage.GetLength(1);
                    SaveArrayAsImage(regionImage, "7.2_superscaled.png");
                }

                // Шаг 3: Предобработка
                bool isLowContrast = IsLowContrast(regionImage);
                int[,] enhanced = isLowContrast ? EnhanceContrast(regionImage) : regionImage;
                SaveArrayAsImage(enhanced, "7.3_contrast_enhanced.png");
                int[,] sharpened = SharpenImage(enhanced);
                SaveArrayAsImage(sharpened, "7.4_sharpened.png");
                int[,] blurred = GaussianBlur(sharpened, sigma: 1.4);
                SaveArrayAsImage(blurred, "7.5_blurred.png");

                // Шаг 4: Комбинированная бинаризация (Оцу + адаптивная)
                int otsuThreshold = ComputeOtsuThreshold(blurred);
                int[,] binary = new int[width, height];
                int windowSize = Math.Max(15, Math.Min(width, height) / 15);
                if (windowSize % 2 == 0) windowSize++;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int x1 = Math.Max(0, x - windowSize / 2);
                        int y1 = Math.Max(0, y - windowSize / 2);
                        int x2 = Math.Min(width - 1, x + windowSize / 2);
                        int y2 = Math.Min(height - 1, y + windowSize / 2);
                        double sum = 0, sumSq = 0;
                        int count = 0;
                        for (int j = y1; j <= y2; j++)
                        {
                            for (int i = x1; i <= x2; i++)
                            {
                                sum += blurred[i, j];
                                sumSq += blurred[i, j] * blurred[i, j];
                                count++;
                            }
                        }
                        double mean = sum / count;
                        double stdDev = Math.Sqrt((sumSq - sum * sum / count) / count);
                        double adaptiveThreshold = mean * (1 + 0.15 * (stdDev / 128 - 1));
                        double blendedThreshold = 0.5 * adaptiveThreshold + 0.5 * otsuThreshold;
                        binary[x, y] = blurred[x, y] > blendedThreshold ? 255 : 0;
                    }
                }
                SaveArrayAsImage(binary, "7.6_binary_region.png");

                // Шаг 5: Извлечение профиля из нескольких строк
                List<int[]> profiles = new List<int[]>();
                int numScans = Math.Min(5, height);
                if (numScans == 0) numScans = 1;
                for (int scan = 0; scan < numScans; scan++)
                {
                    int y = height / (numScans + 1) * (scan + 1);
                    if (y >= height) y = height - 1;
                    int[] profile = new int[width];
                    for (int x = 0; x < width; x++)
                    {
                        profile[x] = binary[x, y] == 255 ? 0 : 1;
                    }
                    profiles.Add(profile);
                }

                // Шаг 6: Выбор лучшего профиля
                int[] bestProfile = SelectBestProfile(profiles);
                SaveProfileAsImage(bestProfile, "8_profile.png");


                return bestProfile;
            }
            catch (IndexOutOfRangeException ex)
            {
                Console.WriteLine($"Index error in GetProfileFromRegion: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in GetProfileFromRegion: {ex.Message}");
                throw;
            }
        }

        private int[,] SuperResolution(int[,] input, int scaleFactor = 2)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int newWidth = width * scaleFactor;
            int newHeight = height * scaleFactor;
            int[,] output = new int[newWidth, newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float srcX = x / (float)scaleFactor;
                    float srcY = y / (float)scaleFactor;
                    int x0 = (int)Math.Floor(srcX);
                    int y0 = (int)Math.Floor(srcY);
                    float dx = srcX - x0;
                    float dy = srcY - y0;

                    int sum = 0, count = 0;
                    for (int ky = -1; ky <= 2; ky++)
                        for (int kx = -1; kx <= 2; kx++)
                        {
                            int nx = x0 + kx;
                            int ny = y0 + ky;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                float weight = BicubicWeight(dx - kx) * BicubicWeight(dy - ky);
                                sum += (int)(input[nx, ny] * weight);
                                count++;
                            }
                        }
                    output[x, y] = Math.Max(0, Math.Min(255, sum / count));
                }
            }

            SaveArrayAsImage(output, "7_superscaled.png");
            return output;
        }

        private float BicubicWeight(float x)
        {
            x = Math.Abs(x);
            if (x <= 1) return 1.5f * x * x * x - 2.5f * x * x + 1;
            if (x < 2) return -0.5f * x * x * x + 2.5f * x * x - 4 * x + 2;
            return 0;
        }

        private int[,] SharpenImage(int[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] output = new int[width, height];

            int[,] kernel = {
                { 0, -1,  0 },
                {-1,  5, -1 },
                { 0, -1,  0 }
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (y > 0 && y < height - 1 && x > 0 && x < width - 1)
                    {
                        int sum = 0;
                        for (int ky = -1; ky <= 1; ky++)
                            for (int kx = -1; kx <= 1; kx++)
                                sum += input[x + kx, y + ky] * kernel[ky + 1, kx + 1];
                        output[x, y] = Math.Max(0, Math.Min(255, sum));
                    }
                    else
                        output[x, y] = input[x, y];
                }
            }

            SaveArrayAsImage(output, "7_sharpened.png");
            return output;
        }


        private int[] SelectBestProfile(List<int[]> profiles)
        {
            int bestScore = 0;
            int[] bestProfile = profiles[0];
            foreach (var profile in profiles)
            {
                int transitions = 0;
                for (int i = 1; i < profile.Length; i++)
                    if (profile[i] != profile[i - 1]) transitions++;
                if (transitions > bestScore)
                {
                    bestScore = transitions;
                    bestProfile = profile;
                }
            }
            return bestProfile;
        }

        private void SaveProfileAsImage(int[] profile, string filename)
        {
            int width = profile.Length;
            int height = 100;
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int x = 0; x < width; x++)
                {
                    int value = Math.Max(0, Math.Min(255, profile[x] * 255));
                    for (int y = 0; y < height; y++)
                    {
                        Color color = Color.FromArgb(value, value, value);
                        bmp.SetPixel(x, y, color);
                    }
                }
                try
                {
                    bmp.Save(filename, ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving {filename}: {ex.Message}");
                }
            }
        }
        // Вспомогательный метод для конвертации Bitmap в base64
        public static string BitmapToBase64(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
