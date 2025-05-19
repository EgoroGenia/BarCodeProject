using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

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

        private int[,] ConvertToGrayscale(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int[,] gray = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = bmp.GetPixel(x, y);
                    int luminance = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                    gray[x, y] = luminance;
                }
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

        private int[,] GaussianBlur(int[,] input)
        {
            int width = _image.Width;
            int height = _image.Height;
            int[,] result = new int[width, height];
            float[] kernel = { 1, 4, 7, 4, 1, 4, 16, 26, 16, 4, 6, 26, 41, 26, 6, 4, 16, 26, 16, 4, 1, 4, 7, 4, 1 };
            float kernelSum = 256;

            for (int y = 2; y < height - 2; y++)
            {
                for (int x = 2; x < width - 2; x++)
                {
                    float sum = 0;
                    int k = 0;
                    for (int ky = -2; ky <= 2; ky++)
                    {
                        for (int kx = -2; kx <= 2; kx++)
                        {
                            sum += input[x + kx, y + ky] * kernel[k];
                            k++;
                        }
                    }
                    result[x, y] = (int)(sum / kernelSum);
                }
            }

            SaveArrayAsImage(result, "2_blurred.png");
            return result;
        }

        // Новый метод: Кэнни для обнаружения краёв
        private int[,] CannyEdgeDetection(int[,] input, int lowThreshold = 50, int highThreshold = 100)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            int[,] result = new int[width, height];

            // Шаг 1: Вычисление градиентов (Собель для горизонтального и вертикального направлений)
            double[,] gradX = new double[width, height];
            double[,] gradY = new double[width, height];
            double[,] magnitude = new double[width, height];
            double[,] direction = new double[width, height];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    // Горизонтальный градиент
                    gradX[x, y] = -input[x - 1, y - 1] - 2 * input[x - 1, y] - input[x - 1, y + 1] +
                                   input[x + 1, y - 1] + 2 * input[x + 1, y] + input[x + 1, y + 1];
                    // Вертикальный градиент
                    gradY[x, y] = -input[x - 1, y - 1] - 2 * input[x, y - 1] - input[x + 1, y - 1] +
                                   input[x - 1, y + 1] + 2 * input[x, y + 1] + input[x + 1, y + 1];
                    // Величина градиента
                    magnitude[x, y] = Math.Sqrt(gradX[x, y] * gradX[x, y] + gradY[x, y] * gradY[x, y]);
                    // Направление градиента (в градусах)
                    direction[x, y] = Math.Atan2(gradY[x, y], gradX[x, y]) * 180 / Math.PI;
                    if (direction[x, y] < 0) direction[x, y] += 360;
                }
            }

            // Шаг 2: Подавление немаксимумов
            int[,] suppressed = new int[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double angle = direction[x, y];
                    double mag = magnitude[x, y];
                    int mag1 = 0, mag2 = 0;

                    // Квантование направления (0°, 45°, 90°, 135°)
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

                    // Если текущий пиксель — максимум в направлении градиента
                    if (mag >= mag1 && mag >= mag2)
                        suppressed[x, y] = (int)mag;
                    else
                        suppressed[x, y] = 0;
                }
            }

            // Шаг 3: Двойная пороговая фильтрация
            int[,] thresholded = new int[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int mag = suppressed[x, y];
                    if (mag >= highThreshold)
                        thresholded[x, y] = 255; // Сильный край
                    else if (mag >= lowThreshold)
                        thresholded[x, y] = 128; // Слабый край
                    else
                        thresholded[x, y] = 0; // Не край
                }
            }

            // Шаг 4: Соединение краёв
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (thresholded[x, y] == 128)
                    {
                        bool connected = false;
                        for (int dy = -1; dy <= 1 && !connected; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (thresholded[x + dx, y + dy] == 255)
                                {
                                    connected = true;
                                    break;
                                }
                            }
                        }
                        result[x, y] = connected ? 255 : 0;
                    }
                    else
                    {
                        result[x, y] = thresholded[x, y];
                    }
                }
            }

            SaveArrayAsImage(result, "3_canny.png");
            return result;
        }

        private int[,] MorphologicalClosing(int[,] input)
        {
            int width = _image.Width;
            int height = _image.Height;
            int[,] dilated = new int[width, height];
            int[,] result = new int[width, height];

            int kw = Math.Max(7, width / 50);
            int kh = Math.Max(3, height / 100);

            for (int y = kh / 2; y < height - kh / 2; y++)
            {
                for (int x = kw / 2; x < width - kw / 2; x++)
                {
                    int maxVal = 0;
                    for (int ky = -kh / 2; ky <= kh / 2; ky++)
                    {
                        for (int kx = -kw / 2; kx <= kw / 2; kx++)
                        {
                            int val = input[x + kx, y + ky];
                            if (val > maxVal) maxVal = val;
                        }
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
                    {
                        for (int kx = -kw / 2; kx <= kw / 2; kx++)
                        {
                            int val = dilated[x + kx, y + ky];
                            if (val < minVal) minVal = val;
                        }
                    }
                    result[x, y] = minVal;
                }
            }

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
            {
                for (int x = 0; x < width; x++)
                {
                    histogram[input[x, y]]++;
                }
            }

            float sum = 0;
            for (int i = 0; i < 256; i++)
            {
                sum += i * histogram[i];
            }
            float sumB = 0;
            int wB = 0;
            int wF = 0;
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

        private int[,] AdaptiveThreshold(int[,] input)
        {
            int width = _image.Width;
            int height = _image.Height;
            int[,] result = new int[width, height];

            // Compute global Otsu threshold for blending
            int otsuThreshold = ComputeOtsuThreshold(input);

            // Sauvola parameters
            int windowSize = Math.Max(15, Math.Min(width, height) / 15);
            if (windowSize % 2 == 0) windowSize++;
            double k = 0.15; // Less aggressive for lighter backgrounds
            double r = 128;
            double minThreshold = otsuThreshold * 0.7; // Minimum threshold to prevent over-whitening

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int x1 = Math.Max(0, x - windowSize / 2);
                    int y1 = Math.Max(0, y - windowSize / 2);
                    int x2 = Math.Min(width - 1, x + windowSize / 2);
                    int y2 = Math.Min(height - 1, y + windowSize / 2);

                    double sum = 0;
                    double sumSq = 0;
                    int count = 0;

                    for (int j = y1; j <= y2; j++)
                    {
                        for (int i = x1; i <= x2; i++)
                        {
                            int pixel = input[i, j];
                            sum += pixel;
                            sumSq += pixel * pixel;
                            count++;
                        }
                    }

                    double mean = sum / count;
                    double variance = (sumSq - sum * sum / count) / count;
                    double stdDev = Math.Sqrt(variance);

                    // Modified Sauvola threshold
                    double sauvolaThreshold = mean * (1 + k * (stdDev / r - 1));
                    // Blend with Otsu to stabilize for lighter backgrounds
                    double blendedThreshold = 0.6 * sauvolaThreshold + 0.4 * otsuThreshold;
                    // Ensure threshold doesn't go too low
                    blendedThreshold = Math.Max(minThreshold, blendedThreshold);

                    result[x, y] = input[x, y] > blendedThreshold ? 255 : 0;
                }
            }

            SaveArrayAsImage(result, "5_threshold_improved.png");
            return result;
        }

        private Rectangle[] FindContours(int[,] binary)
        {
            int width = _image.Width;
            int height = _image.Height;
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
                        stack.Push(new Point(x, y));
                        visited[x, y] = true;
                        int whitePixelCount = 1;

                        while (stack.Count > 0)
                        {
                            var p = stack.Pop();
                            if (p.X < minX) minX = p.X;
                            if (p.X > maxX) maxX = p.X;
                            if (p.Y < minY) minY = p.Y;
                            if (p.Y > maxY) maxY = p.Y;

                            for (int dy = -1; dy <= 1; dy++)
                            {
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
                        }

                        int w = maxX - minX + 1;
                        int h = maxY - minY + 1;
                        int minWidth = width / 20;
                        int minHeight = height / 40;
                        double fillRatio = whitePixelCount / (double)(w * h);

                        if (w > minWidth && h > minHeight && w / (float)h > 1.5 && fillRatio > 0.3)
                        {
                            rectangles.Add(new Rectangle(minX, minY, w, h));
                        }
                    }
                }
            }

            var mergedRectangles = MergeCloseRectangles(rectangles, binary, width / 20, height / 20);
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

                for (int y = Math.Max(0, current.Y); y < Math.Min(_image.Height, current.Y + current.Height); y++)
                {
                    for (int x = Math.Max(0, current.X); x < Math.Min(_image.Width, current.X + current.Width); x++)
                    {
                        if (binary[x, y] == 255)
                        {
                            whitePixelCount++;
                        }
                    }
                }

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

                        for (int y = Math.Max(0, other.Y); y < Math.Min(_image.Height, other.Y + other.Height); y++)
                        {
                            for (int x = Math.Max(0, other.X); x < Math.Min(_image.Width, other.X + other.Width); x++)
                            {
                                if (binary[x, y] == 255)
                                {
                                    whitePixelCount++;
                                }
                            }
                        }

                        used[j] = true;
                    }
                }

                int w = maxX - minX + 1;
                int h = maxY - minY + 1;
                double newFillRatio = whitePixelCount / (double)(w * h);

                if (w / (float)h > 1.5 && newFillRatio > 0.3)
                {
                    merged.Add(new Rectangle(minX, minY, w, h));
                }
            }

            return merged;
        }

        private void SaveContoursImage(int[,] binary, List<Rectangle> rectangles, string filename)
        {
            int width = _image.Width;
            int height = _image.Height;
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int value = binary[x, y];
                        Color color = value == 255 ? Color.White : Color.Black;
                        bitmap.SetPixel(x, y, color);
                    }
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
                {
                    for (int x = 0; x < width; x++)
                    {
                        int value = data[x, y];
                        if (value < 0) value = 0;
                        if (value > 255) value = 255;
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

        private void SaveRegionAsImage(int[,] data, Rectangle region, string filename)
        {
            int width = region.Width;
            int height = region.Height;
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int value = data[region.X + x, region.Y + y];
                        if (value < 0) value = 0;
                        if (value > 255) value = 255;
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

        private int[,] ApplyCLAHE(int[,] input, Rectangle region)
        {
            int width = region.Width;
            int height = region.Height;
            int[,] result = new int[_image.Width, _image.Height];
            int tileSize = 8;
            int clipLimit = 2;

            for (int ty = 0; ty < height; ty += tileSize)
            {
                for (int tx = 0; tx < width; tx += tileSize)
                {
                    int[] histogram = new int[256];
                    int pixelCount = 0;

                    for (int y = ty; y < Math.Min(ty + tileSize, height); y++)
                    {
                        for (int x = tx; x < Math.Min(tx + tileSize, width); x++)
                        {
                            int value = input[region.X + x, region.Y + y];
                            histogram[value]++;
                            pixelCount++;
                        }
                    }

                    int clipThreshold = pixelCount / 256 * clipLimit;
                    int clippedPixels = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        if (histogram[i] > clipThreshold)
                        {
                            clippedPixels += histogram[i] - clipThreshold;
                            histogram[i] = clipThreshold;
                        }
                    }
                    int perBin = clippedPixels / 256;
                    for (int i = 0; i < 256; i++)
                    {
                        histogram[i] += perBin;
                    }

                    int[] cdf = new int[256];
                    cdf[0] = histogram[0];
                    for (int i = 1; i < 256; i++)
                    {
                        cdf[i] = cdf[i - 1] + histogram[i];
                    }
                    int cdfMin = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        if (cdf[i] > 0)
                        {
                            cdfMin = cdf[i];
                            break;
                        }
                    }
                    int cdfMax = cdf[255];

                    for (int y = ty; y < Math.Min(ty + tileSize, height); y++)
                    {
                        for (int x = tx; x < Math.Min(tx + tileSize, width); x++)
                        {
                            int value = input[region.X + x, region.Y + y];
                            int newValue = cdfMax == cdfMin ? value : ((cdf[value] - cdfMin) * 255) / (cdfMax - cdfMin);
                            result[region.X + x, region.Y + y] = newValue;
                        }
                    }
                }
            }

            return result;
        }

        private int[,] OtsuThreshold(int[,] input, Rectangle region)
        {
            int width = region.Width;
            int height = region.Height;
            int[,] result = new int[_image.Width, _image.Height];

            int[] histogram = new int[256];
            int totalPixels = width * height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    histogram[input[region.X + x, region.Y + y]]++;
                }
            }

            float sum = 0;
            for (int i = 0; i < 256; i++)
            {
                sum += i * histogram[i];
            }
            float sumB = 0;
            int wB = 0;
            int wF = 0;
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

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int value = input[region.X + x, region.Y + y];
                    result[region.X + x, region.Y + y] = value >= threshold ? 255 : 0;
                }
            }

            return result;
        }

        // Изменено: Используем CannyEdgeDetection вместо SobelHorizontal
        public Rectangle DetectBarcodeRegion()
        {
            int[,] enhanced = EnhanceContrast(_grayImage);
            bool isLowContrast = IsLowContrast(_grayImage);

            int[,] blurred = GaussianBlur(isLowContrast ? enhanced : _grayImage);
            int[,] edges = CannyEdgeDetection(blurred, 50, 100);
            int[,] closed = MorphologicalClosing(edges);
            int[,] thresh = AdaptiveThreshold(closed);

            var contours = FindContours(thresh);

            if (contours.Length == 0)
            {
                if (isLowContrast)
                {
                    int[,] highPass = ApplyHighPassFilter(enhanced);
                    edges = CannyEdgeDetection(highPass, 50, 100);
                    closed = MorphologicalClosing(edges);
                    thresh = AdaptiveThreshold(edges);
                    contours = FindContours(thresh);
                }
                _barcodeRegion = Rectangle.Empty;
            }
            else
            {
                _barcodeRegion = contours.OrderByDescending(r => r.Width * r.Height).First();
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
                    {
                        g.DrawRectangle(pen, _barcodeRegion);
                    }
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
            if (region == Rectangle.Empty)
                return new int[0];

            // Ensure region is within image bounds
            region = Rectangle.Intersect(region, new Rectangle(0, 0, _image.Width, _image.Height));
            if (region.IsEmpty)
                return new int[0];

            // Extract region of interest (ROI)
            int[,] roi = ExtractROI(_grayImage, region);
            SaveArrayAsImage(roi, "7_roi.png");

            // Step 1: Enhance contrast to improve binarization
            int[,] enhanced = EnhanceContrast(roi, region.Width, region.Height);
            SaveArrayAsImage(enhanced, "8_enhanced.png");

            // Step 2: Denoise with bilateral filter to preserve edges
            int[,] denoised = ApplyBilateralFilter(enhanced, region.Width, region.Height);
            SaveArrayAsImage(denoised, "9_denoised.png");

            // Step 3: Robust binarization with adaptive and global thresholding
            int[,] binary = ApplyRobustBinarization(denoised, region.Width, region.Height);
            SaveArrayAsImage(binary, "10_binary.png");

            // Step 4: Repair broken bars with morphological operations
            int[,] repaired = RepairBrokenBars(binary, region.Width, region.Height);
            SaveArrayAsImage(repaired, "11_repaired.png");

            // Step 5: Correct for potential inversion (ensure bars are dark)
            if (IsImageInverted(repaired, region.Width, region.Height))
            {
                repaired = InvertBinaryImage(repaired, region.Width, region.Height);
                SaveArrayAsImage(repaired, "11a_inverted.png");
            }

            // Step 6: Find barcode bounds (first and last black bars)
            (int leftBound, int rightBound) = FindBarcodeBounds(repaired, region.Width, region.Height);
            if (leftBound >= rightBound)
            {
                Console.WriteLine("Warning: Could not find valid barcode bounds. Using full region.");
                leftBound = 0;
                rightBound = region.Width - 1;
            }
            int newWidth = rightBound - leftBound + 1;
            Console.WriteLine($"Barcode bounds: left={leftBound}, right={rightBound}, newWidth={newWidth}");

            // Crop the repaired image to the barcode bounds
            int[,] cropped = new int[newWidth, region.Height];
            for (int y = 0; y < region.Height; y++)
                for (int x = 0; x < newWidth; x++)
                    cropped[x, y] = repaired[x + leftBound, y];
            SaveArrayAsImage(cropped, "11b_cropped.png");

            // Step 7: Deskew the barcode using improved skew detection
            double angle = DetectSkewAngle(cropped, newWidth, region.Height);
            int[,] deskewed = RotateImage(cropped, newWidth, region.Height, -angle);
            SaveArrayAsImage(deskewed, "12_deskewed.png");

            // Step 8: Generate intensity profile with robustness to distortions
            int[] profile = GenerateRobustProfile(deskewed, newWidth, region.Height);
            SaveProfileAsImage(profile, "13_profile.png");

            return profile;
        }

        private int EstimateMinSpaceWidth(int[,] input, int width, int height)
        {
            // Сканируем несколько строк (не только середину) для большей точности
            int numScanlines = Math.Max(3, height / 20);
            int step = height / (numScanlines + 1);
            List<int> spaceWidths = new List<int>();

            for (int s = 1; s <= numScanlines; s++)
            {
                int y = s * step;
                int currentSpaceWidth = 0;
                bool inSpace = input[0, y] == 255;

                for (int x = 0; x < width; x++)
                {
                    if (input[x, y] == 255 && inSpace)
                    {
                        currentSpaceWidth++;
                    }
                    else if (input[x, y] == 0 && inSpace)
                    {
                        if (currentSpaceWidth > 0)
                            spaceWidths.Add(currentSpaceWidth);
                        inSpace = false;
                        currentSpaceWidth = 0;
                    }
                    else if (input[x, y] == 255 && !inSpace)
                    {
                        inSpace = true;
                        currentSpaceWidth = 1;
                    }
                }
                if (inSpace && currentSpaceWidth > 0)
                    spaceWidths.Add(currentSpaceWidth);
            }

            // Возвращаем минимальную ширину пробела или значение по умолчанию
            int defaultWidth = Math.Max(1, width / 200); // Меньше для маленьких изображений
            int minSpaceWidth = spaceWidths.Any() ? spaceWidths.Min() : defaultWidth;

            // Ограничиваем minSpaceWidth для маленьких изображений
            minSpaceWidth = Math.Min(minSpaceWidth, Math.Max(3, width / 50));
            Console.WriteLine($"minSpaceWidth: {minSpaceWidth}, image width: {width}, height: {height}");
            return minSpaceWidth;
        }

        private int[,] RepairBrokenBars(int[,] input, int width, int height)
        {
            int[,] result = new int[width, height];

            // Оцениваем минимальную ширину пробела
            int minSpaceWidth = EstimateMinSpaceWidth(input, width, height);

            // Адаптивный размер ядра, меньше для маленьких изображений
            int kernelWidth = Math.Max(1, minSpaceWidth / 3); // Еще меньше ядро
            int kernelHeight = Math.Max(1, height / 300); // Минимальная высота
            if (kernelWidth % 2 == 0) kernelWidth++;
            if (kernelHeight % 2 == 0) kernelHeight++;

            // Ограничиваем ядро для маленьких изображений
            kernelWidth = Math.Min(kernelWidth, 3); // Не больше 3 для маленьких изображений
            kernelHeight = Math.Min(kernelHeight, 3);
            Console.WriteLine($"kernelWidth: {kernelWidth}, kernelHeight: {kernelHeight}");

            // Дилатация: расширяем черные области (0)
            int[,] dilated = new int[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (y < kernelHeight / 2 || y >= height - kernelHeight / 2 ||
                        x < kernelWidth / 2 || x >= width - kernelWidth / 2)
                    {
                        dilated[x, y] = input[x, y]; // Копируем границы
                        continue;
                    }

                    int minVal = 255;
                    for (int ky = -kernelHeight / 2; ky <= kernelHeight / 2; ky++)
                        for (int kx = -kernelWidth / 2; kx <= kernelWidth / 2; kx++)
                            minVal = Math.Min(minVal, input[x + kx, y + ky]);
                    dilated[x, y] = minVal;
                }
            }
            SaveArrayAsImage(dilated, "11_dilated.png");

            // Эрозия: восстанавливаем пробелы
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (y < kernelHeight / 2 || y >= height - kernelHeight / 2 ||
                        x < kernelWidth / 2 || x >= width - kernelWidth / 2)
                    {
                        result[x, y] = dilated[x, y]; // Копируем границы
                        continue;
                    }

                    int maxVal = 0;
                    for (int ky = -kernelHeight / 2; ky <= kernelHeight / 2; ky++)
                        for (int kx = -kernelWidth / 2; kx <= kernelWidth / 2; kx++)
                            maxVal = Math.Max(maxVal, dilated[x + kx, y + ky]);
                    result[x, y] = maxVal;
                }
            }

            return result;
        }

        private int[,] ExtractROI(int[,] input, Rectangle region)
        {
            int[,] roi = new int[region.Width, region.Height];
            for (int y = 0; y < region.Height; y++)
                for (int x = 0; x < region.Width; x++)
                    roi[x, y] = input[region.X + x, region.Y + y];
            return roi;
        }

        private int[,] EnhanceContrast(int[,] input, int width, int height)
        {
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
                for (int x = 0; x < width; x++)
                {
                    int value = input[x, y];
                    int newValue = (int)((cdf[value] - cdfMin) * 255f / (totalPixels - cdfMin));
                    output[x, y] = Math.Max(0, Math.Min(255, newValue));
                }
            return output;
        }

        private int[,] ApplyBilateralFilter(int[,] input, int width, int height)
        {
            int[,] result = new int[width, height];
            int sigmaSpatial = Math.Max(5, Math.Min(width, height) / 50);
            int sigmaRange = 30;
            int kernelSize = sigmaSpatial * 3;
            if (kernelSize % 2 == 0) kernelSize++;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sum = 0;
                    double weightSum = 0;

                    for (int ky = -kernelSize / 2; ky <= kernelSize / 2; ky++)
                    {
                        for (int kx = -kernelSize / 2; kx <= kernelSize / 2; kx++)
                        {
                            int nx = Math.Max(0, Math.Min(width - 1, x + kx));
                            int ny = Math.Max(0, Math.Min(height - 1, y + ky));
                            int intensityDiff = input[nx, ny] - input[x, y];
                            double spatialDist = Math.Sqrt(kx * kx + ky * ky);

                            double spatialWeight = Math.Exp(-spatialDist * spatialDist / (2 * sigmaSpatial * sigmaSpatial));
                            double rangeWeight = Math.Exp(-intensityDiff * intensityDiff / (2 * sigmaRange * sigmaRange));
                            double weight = spatialWeight * rangeWeight;

                            sum += input[nx, ny] * weight;
                            weightSum += weight;
                        }
                    }

                    result[x, y] = (int)(sum / weightSum);
                }
            }
            return result;
        }

        private int[,] ApplyRobustBinarization(int[,] input, int width, int height)
        {
            int[,] result = new int[width, height];
            int windowSize = Math.Max(15, Math.Min(width, height) / 15);
            if (windowSize % 2 == 0) windowSize++;
            double k = 0.15;
            double r = 128;

            int globalThreshold = ComputeOtsuThreshold(input);
            double globalVariance = ComputeImageVariance(input, width, height);
            bool useGlobal = globalVariance < 100;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (useGlobal)
                    {
                        result[x, y] = input[x, y] > globalThreshold ? 255 : 0;
                    }
                    else
                    {
                        int x1 = Math.Max(0, x - windowSize / 2);
                        int y1 = Math.Max(0, y - windowSize / 2);
                        int x2 = Math.Min(width, x + windowSize / 2 + 1);
                        int y2 = Math.Min(height, y + windowSize / 2 + 1);

                        double sum = 0, sumSq = 0;
                        int count = 0;
                        for (int j = y1; j < y2; j++)
                            for (int i = x1; i < x2; i++)
                            {
                                int pixel = input[i, j];
                                sum += pixel;
                                sumSq += pixel * pixel;
                                count++;
                            }

                        double mean = sum / count;
                        double variance = (sumSq - sum * sum / count) / count;
                        double stdDev = Math.Sqrt(variance);

                        double sauvolaThreshold = mean * (1 + k * (stdDev / r - 1));
                        double threshold = variance < 50 ? globalThreshold : 0.6 * sauvolaThreshold + 0.4 * globalThreshold;
                        result[x, y] = input[x, y] > threshold ? 255 : 0;
                    }
                }
            }
            return result;
        }

        private double ComputeImageVariance(int[,] input, int width, int height)
        {
            double sum = 0, sumSq = 0;
            int count = width * height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int pixel = input[x, y];
                    sum += pixel;
                    sumSq += pixel * pixel;
                }
            double mean = sum / count;
            return (sumSq - sum * sum / count) / count;
        }

        private bool IsImageInverted(int[,] binary, int width, int height)
        {
            int blackCount = 0, total = width * height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (binary[x, y] == 0)
                        blackCount++;
            return blackCount < total / 2;
        }

        private int[,] InvertBinaryImage(int[,] binary, int width, int height)
        {
            int[,] result = new int[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result[x, y] = binary[x, y] == 0 ? 255 : 0;
            return result;
        }

        private double DetectSkewAngle(int[,] binary, int width, int height)
        {
            int maxTheta = 90;
            int maxRho = (int)Math.Sqrt(width * width + height * height);
            int[,] houghSpace = new int[maxTheta, maxRho * 2];
            int minVotes = Math.Max(15, Math.Min(width, height) / 10);

            int[,] edges = CannyEdgeDetection(binary, 50, 100);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (edges[x, y] == 255)
                        for (int theta = 0; theta < maxTheta; theta++)
                        {
                            double rad = theta * Math.PI / 180;
                            int rho = (int)(x * Math.Cos(rad) + y * Math.Sin(rad));
                            houghSpace[theta, rho + maxRho]++;
                        }

            List<(int Theta, int Votes)> topAngles = new List<(int, int)>();
            for (int theta = 0; theta < maxTheta; theta++)
                for (int rho = 0; rho < maxRho * 2; rho++)
                    if (houghSpace[theta, rho] > minVotes)
                        topAngles.Add((theta, houghSpace[theta, rho]));

            if (topAngles.Count == 0)
                return 0;

            var sortedAngles = topAngles.OrderByDescending(a => a.Votes).Take(5).ToList();
            double avgAngle = 0;
            int totalVotes = 0;
            foreach (var (theta, votes) in sortedAngles)
            {
                avgAngle += theta * votes;
                totalVotes += votes;
            }
            double angle = totalVotes > 0 ? avgAngle / totalVotes : 0;

            double finalAngle = angle;
            if (finalAngle > 45)
                finalAngle -= 90;
            else if (finalAngle < -45)
                finalAngle += 90;

            if (Math.Abs(finalAngle) > 30)
                finalAngle = 0;

            return finalAngle;
        }

        private int[,] RotateImage(int[,] input, int width, int height, double angle)
        {
            int[,] result = new int[width, height];
            double rad = angle * Math.PI / 180;
            int cx = width / 2, cy = height / 2;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int srcX = (int)((x - cx) * Math.Cos(rad) + (y - cy) * Math.Sin(rad) + cx);
                    int srcY = (int)(-(x - cx) * Math.Sin(rad) + (y - cy) * Math.Cos(rad) + cy);
                    result[x, y] = (srcX >= 0 && srcX < width && srcY >= 0 && srcY < height)
                        ? input[srcX, srcY]
                        : 255;
                }
            return result;
        }

        private int[] GenerateRobustProfile(int[,] binary, int width, int height)
        {
            int numScanlines = Math.Max(5, height / 20);
            int step = height / (numScanlines + 1);
            List<int[]> scanlines = new List<int[]>();

            for (int s = 1; s <= numScanlines; s++)
            {
                int y = s * step;
                int[] scanline = new int[width];
                for (int x = 0; x < width; x++)
                    scanline[x] = binary[x, y];
                scanlines.Add(scanline);
            }

            int[] profile = new int[width];
            for (int x = 0; x < width; x++)
            {
                int[] values = new int[scanlines.Count];
                for (int s = 0; s < scanlines.Count; s++)
                    values[s] = scanlines[s][x];
                Array.Sort(values);
                profile[x] = values[values.Length / 2];
            }

            int[] smoothed = new int[width];
            int windowSize = Math.Max(3, width / 100);
            if (windowSize % 2 == 0) windowSize++;
            for (int x = 0; x < width; x++)
            {
                int sum = 0, count = 0;
                for (int k = -windowSize / 2; k <= windowSize / 2; k++)
                {
                    int nx = Math.Max(0, Math.Min(width - 1, x + k));
                    sum += profile[nx];
                    count++;
                }
                smoothed[x] = sum / count;
            }

            return smoothed;
        }


        private void SaveProfileAsImage(int[] profile, string filename)
        {
            int width = profile.Length;
            int height = 100;
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                for (int x = 0; x < width; x++)
                {
                    int value = profile[x];
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

        private (int leftBound, int rightBound) FindBarcodeBounds(int[,] input, int width, int height)
        {
            int leftBound = -1, rightBound = -1;
            int blackThreshold = height / 2; // Требуется не менее 50% черных пикселей в столбце

            // Найти левую границу (первую черную полосу)
            for (int x = 0; x < width; x++)
            {
                int blackCount = 0;
                for (int y = 0; y < height; y++)
                    if (input[x, y] == 0)
                        blackCount++;
                if (blackCount >= blackThreshold)
                {
                    leftBound = x;
                    break;
                }
            }

            // Найти правую границу (последнюю черную полосу)
            for (int x = width - 1; x >= 0; x--)
            {
                int blackCount = 0;
                for (int y = 0; y < height; y++)
                    if (input[x, y] == 0)
                        blackCount++;
                if (blackCount >= blackThreshold)
                {
                    rightBound = x;
                    break;
                }
            }

            // Если границы не найдены, вернуть полную ширину
            if (leftBound == -1 || rightBound == -1)
                return (0, width - 1);

            return (leftBound, rightBound);
        }

    }
}