using System;
using System.Drawing;
using System.Collections.Generic;

namespace BarcodeDecoderWinForms
{
    public class BarcodeImage
    {
        private Bitmap _bitmap;

        public BarcodeImage(string imagePath)
        {
            _bitmap = new Bitmap(imagePath);
        }

        // Преобразование изображения в градации серого
        private int[,] ToGrayscale()
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] grayscale = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color pixel = _bitmap.GetPixel(x, y);
                    grayscale[x, y] = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);
                }
            }
            return grayscale;
        }

        // Фильтр Собеля для вычисления градиентов
        private (double[,] magnitude, double[,] direction) ApplySobelFilter(int[,] grayscale)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            double[,] magnitude = new double[width, height];
            double[,] direction = new double[width, height];

            int[,] sobelX = new int[3, 3] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] sobelY = new int[3, 3] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    double gx = 0, gy = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int pixel = grayscale[x + dx, y + dy];
                            gx += pixel * sobelX[dx + 1, dy + 1];
                            gy += pixel * sobelY[dx + 1, dy + 1];
                        }
                    }
                    magnitude[x, y] = Math.Sqrt(gx * gx + gy * gy);
                    direction[x, y] = Math.Atan2(gy, gx) * 180 / Math.PI;
                    if (direction[x, y] < 0) direction[x, y] += 180;
                }
            }
            return (magnitude, direction);
        }

        // Размытие (Blur) для сглаживания (работает с double[,])
        private double[,] ApplyBlur(double[,] input, int kernelSize = 5)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            double[,] blurred = new double[width, height];
            int offset = kernelSize / 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double sum = 0;
                    int count = 0;
                    for (int dx = -offset; dx <= offset; dx++)
                    {
                        for (int dy = -offset; dy <= offset; dy++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            sum += input[nx, ny];
                            count++;
                        }
                    }
                    blurred[x, y] = sum / count;
                }
            }
            return blurred;
        }

        // Пороговая бинаризация
        private int[,] ApplyThreshold(double[,] magnitude, double threshold)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] binary = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    binary[x, y] = magnitude[x, y] > threshold ? 1 : 0;
                }
            }
            return binary;
        }

        // Морфологическое закрытие (Closing)
        private int[,] ApplyMorphologicalClosing(int[,] binary, int kernelSize = 5)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int offset = kernelSize / 2;

            // Шаг 1: Дилатация (расширение)
            int[,] dilated = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool hasOne = false;
                    for (int dx = -offset; dx <= offset; dx++)
                    {
                        for (int dy = -offset; dy <= offset; dy++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            if (binary[nx, ny] == 1)
                            {
                                hasOne = true;
                                break;
                            }
                        }
                        if (hasOne) break;
                    }
                    dilated[x, y] = hasOne ? 1 : 0;
                }
            }

            // Шаг 2: Эрозия (сужение)
            int[,] closed = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool allOnes = true;
                    for (int dx = -offset; dx <= offset; dx++)
                    {
                        for (int dy = -offset; dy <= offset; dy++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            if (dilated[nx, ny] == 0)
                            {
                                allOnes = false;
                                break;
                            }
                        }
                        if (!allOnes) break;
                    }
                    closed[x, y] = allOnes ? 1 : 0;
                }
            }
            return closed;
        }

        // Поиск контуров и выбор подходящих
        private List<Rectangle> FindContours(int[,] binary)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            bool[,] visited = new bool[width, height];
            List<Rectangle> contours = new List<Rectangle>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (binary[x, y] == 1 && !visited[x, y])
                    {
                        int minX = x, maxX = x, minY = y, maxY = y;
                        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
                        queue.Enqueue((x, y));
                        visited[x, y] = true;

                        while (queue.Count > 0)
                        {
                            var (cx, cy) = queue.Dequeue();
                            minX = Math.Min(minX, cx);
                            maxX = Math.Max(maxX, cx);
                            minY = Math.Min(minY, cy);
                            maxY = Math.Max(maxY, cy);

                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    int nx = cx + dx;
                                    int ny = cy + dy;
                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && binary[nx, ny] == 1 && !visited[nx, ny])
                                    {
                                        queue.Enqueue((nx, ny));
                                        visited[nx, ny] = true;
                                    }
                                }
                            }
                        }

                        Rectangle contour = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        if (contour.Width > contour.Height && contour.Width > 50 && contour.Height > 10)
                        {
                            contours.Add(contour);
                        }
                    }
                }
            }

            contours.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            return contours;
        }

        // Обнаружение штрих-кода
        public Rectangle DetectBarcodeRegion()
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;

            // Шаг 1: Преобразование в градации серого
            int[,] grayscale = ToGrayscale();

            // Шаг 2: Вычисление градиентов с помощью фильтра Собеля
            var (magnitude, direction) = ApplySobelFilter(grayscale);

            // Шаг 3: Сглаживание изображения
            magnitude = ApplyBlur(magnitude, 5);

            // Шаг 4: Бинаризация
            double threshold = 50;
            int[,] binary = ApplyThreshold(magnitude, threshold);

            // Шаг 5: Морфологическое закрытие
            binary = ApplyMorphologicalClosing(binary, 5);

            // Шаг 6: Поиск контуров
            List<Rectangle> contours = FindContours(binary);

            // Шаг 7: Выбор области штрих-кода
            if (contours.Count == 0)
                return Rectangle.Empty;

            Rectangle roi = contours[0];
            int padding = 5;
            roi = new Rectangle(
                Math.Max(0, roi.Left - padding),
                Math.Max(0, roi.Top - padding),
                Math.Min(width - roi.Left, roi.Width + 2 * padding),
                Math.Min(height - roi.Top, roi.Height + 2 * padding)
            );

            return roi;
        }

        // Получение профиля из региона
        public int[] GetProfileFromRegion(Rectangle region)
        {
            int[] profile = new int[region.Width];
            int[,] grayscale = ToGrayscale();
            int[,] blurred = ApplyBlur(grayscale, 5);
            double threshold = 128;

            for (int x = 0; x < region.Width; x++)
            {
                int sum = 0;
                int count = 0;
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    int adjustedX = region.Left + x;
                    if (adjustedX >= 0 && adjustedX < _bitmap.Width && y >= 0 && y < _bitmap.Height)
                    {
                        sum += blurred[adjustedX, y];
                        count++;
                    }
                }
                int avg = count > 0 ? sum / count : 0;
                profile[x] = avg < threshold ? 1 : 0;
            }
            return profile;
        }

        // Размытие для int[,] (для совместимости с GetProfileFromRegion)
        private int[,] ApplyBlur(int[,] input, int kernelSize = 5)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] blurred = new int[width, height];
            int offset = kernelSize / 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int sum = 0;
                    int count = 0;
                    for (int dx = -offset; dx <= offset; dx++)
                    {
                        for (int dy = -offset; dy <= offset; dy++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            sum += input[nx, ny];
                            count++;
                        }
                    }
                    blurred[x, y] = sum / count;
                }
            }
            return blurred;
        }

        public Bitmap HighlightBarcode()
        {
            Rectangle region = DetectBarcodeRegion();
            if (region == Rectangle.Empty) return new Bitmap(_bitmap);

            Bitmap highlighted = new Bitmap(_bitmap);
            using (Graphics g = Graphics.FromImage(highlighted))
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawRectangle(pen, region);
                }
            }
            return highlighted;
        }
    }
}