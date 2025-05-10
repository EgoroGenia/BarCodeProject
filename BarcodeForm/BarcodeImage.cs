using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

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

        // Медианный фильтр для устранения импульсного шума
        private int[,] ApplyMedianFilter(int[,] input, int kernelSize = 5)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] filtered = new int[width, height];
            int offset = kernelSize / 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    List<int> neighbors = new List<int>();
                    for (int dx = -offset; dx <= offset; dx++)
                    {
                        for (int dy = -offset; dy <= offset; dy++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            neighbors.Add(input[nx, ny]);
                        }
                    }
                    neighbors.Sort();
                    filtered[x, y] = neighbors[neighbors.Count / 2];
                }
            }
            return filtered;
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

        // Размытие (Blur) для сглаживания
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

        // Морфологическое закрытие (Дилатация → Эрозия)
        private int[,] ApplyMorphologicalClosing(int[,] binary, int kernelSize = 5)
        {
            int[,] dilated = ApplyDilation(binary, kernelSize);
            return ApplyErosion(dilated, kernelSize);
        }

        // Эрозия
        private int[,] ApplyErosion(int[,] binary, int kernelSize)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] eroded = new int[width, height];
            int offset = kernelSize / 2;

            for (int x = 0; x < width; x++)
            {
                int xMin = Math.Max(x - offset, 0);
                int xMax = Math.Min(x + offset, width - 1);
                for (int y = 0; y < height; y++)
                {
                    int yMin = Math.Max(y - offset, 0);
                    int yMax = Math.Min(y + offset, height - 1);
                    bool allOnes = true;
                    for (int nx = xMin; nx <= xMax && allOnes; nx++)
                    {
                        for (int ny = yMin; ny <= yMax; ny++)
                        {
                            if (binary[nx, ny] == 0)
                            {
                                allOnes = false;
                                break;
                            }
                        }
                    }
                    eroded[x, y] = allOnes ? 1 : 0;
                }
            }
            return eroded;
        }

        // Дилатация
        private int[,] ApplyDilation(int[,] binary, int kernelSize)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] dilated = new int[width, height];
            int offset = kernelSize / 2;

            for (int x = 0; x < width; x++)
            {
                int xMin = Math.Max(x - offset, 0);
                int xMax = Math.Min(x + offset, width - 1);
                for (int y = 0; y < height; y++)
                {
                    int yMin = Math.Max(y - offset, 0);
                    int yMax = Math.Min(y + offset, height - 1);
                    bool hasOne = false;
                    for (int nx = xMin; nx <= xMax && !hasOne; nx++)
                    {
                        for (int ny = yMin; ny <= yMax; ny++)
                        {
                            if (binary[nx, ny] == 1)
                            {
                                hasOne = true;
                                break;
                            }
                        }
                    }
                    dilated[x, y] = hasOne ? 1 : 0;
                }
            }
            return dilated;
        }

        // Морфологическое открытие (Эрозия → Дилатация)
        private int[,] ApplyMorphologicalOpening(int[,] binary, int kernelSize = 15)
        {
            int[,] eroded = ApplyErosion(binary, kernelSize);
            return ApplyDilation(eroded, kernelSize);
        }

        // Адаптивный расчет порога (метод Отсу)
        private double CalculateAdaptiveThreshold(int[,] grayscale)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[] histogram = new int[256];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    histogram[grayscale[x, y]]++;

            int total = width * height;
            double sum = 0;
            for (int i = 0; i < 256; i++)
                sum += i * histogram[i];

            double sumB = 0, wB = 0, maxVariance = 0;
            int threshold = 0;
            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;

                double wF = total - wB;
                if (wF == 0) break;

                sumB += t * histogram[t];
                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;

                double variance = wB * wF * (mB - mF) * (mB - mF);
                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = t;
                }
            }
            return threshold;
        }

        // Адаптивный расчет порога для double[,]
        private double CalculateAdaptiveThreshold(double[,] magnitude)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[] histogram = new int[256];
            double maxMagnitude = magnitude.Cast<double>().Max();
            if (maxMagnitude == 0) maxMagnitude = 1;

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int value = (int)(magnitude[x, y] / maxMagnitude * 255);
                    histogram[value]++;
                }

            int total = width * height;
            double sum = 0;
            for (int i = 0; i < 256; i++)
                sum += i * histogram[i];

            double sumB = 0, wB = 0, maxVariance = 0, threshold = 0;
            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;

                double wF = total - wB;
                if (wF == 0) break;

                sumB += t * histogram[t];
                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;

                double variance = wB * wF * (mB - mF) * (mB - mF);
                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = t;
                }
            }

            double otsuThreshold = (threshold / 255.0) * maxMagnitude;
            double mean = sum / total / 255.0 * maxMagnitude;
            if (otsuThreshold < mean * 0.5 || otsuThreshold > mean * 2.0)
                return mean * 1.5;

            return otsuThreshold;
        }

        // Поиск контуров
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
                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                        binary[nx, ny] == 1 && !visited[nx, ny])
                                    {
                                        queue.Enqueue((nx, ny));
                                        visited[nx, ny] = true;
                                    }
                                }
                            }
                        }

                        Rectangle contour = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        float aspectRatio = (float)contour.Width / contour.Height;

                        if (contour.Width * contour.Height > 100 && // Минимальная площадь
                            contour.Width < width * 0.75 && contour.Height < height * 0.75 && // Максимальный размер
                            aspectRatio > 1.5f && aspectRatio < 10.0f) // Соотношение сторон
                        {
                            int transitions = CountTransitions(binary, contour);
                            if (transitions > 10) // Более строгий порог
                                contours.Add(contour);
                        }
                    }
                }
            }

            contours.Sort((a, b) =>
                (CountTransitions(binary, b) * b.Width * b.Height).CompareTo(
                 CountTransitions(binary, a) * a.Width * a.Height));

            return contours;
        }

        // Подсчет переходов черное-белое
        private int CountTransitions(int[,] binary, Rectangle rect)
        {
            if (rect.Top < 0 || rect.Top >= _bitmap.Height || rect.Left < 0 || rect.Right > _bitmap.Width)
                return 0;

            int totalTransitions = 0;
            int linesToCheck = 3; // Проверяем 3 линии
            int step = rect.Height / (linesToCheck + 1);

            for (int i = 1; i <= linesToCheck; i++)
            {
                int y = rect.Top + step * i;
                if (y >= _bitmap.Height) y = _bitmap.Height - 1;
                int prev = binary[Math.Min(rect.Left, _bitmap.Width - 1), y];
                int transitions = 0;

                for (int x = rect.Left + 1; x < rect.Right; x++)
                {
                    if (x >= _bitmap.Width) break;
                    int current = binary[x, y];
                    if (current != prev)
                    {
                        transitions++;
                        prev = current;
                    }
                }
                totalTransitions += transitions;
            }

            return totalTransitions / linesToCheck;
        }

        // Основной метод для комбинированного подхода
        public Rectangle DetectBarcodeRegionCombined()
        {
            // Шаг 1: Бинаризация с выделением светлых областей
            int[,] binary = GetInvertedBinaryImage();
            SaveBinary("1_binary_combined.png", binary);

            // Шаг 2: Морфологическое открытие для удаления штрихов
            int[,] opened = ApplyMorphologicalOpening(binary, kernelSize: 15);
            SaveBinary("2_opened_combined.png", opened);

            // Шаг 3: Поиск наибольшей белой области
            Rectangle largestWhiteRegion = FindLargestWhiteRegion(opened);
            SaveContoursImage("3_contours_combined.png", new List<Rectangle> { largestWhiteRegion });

            if (largestWhiteRegion == Rectangle.Empty)
                return Rectangle.Empty;

            // Шаг 4: Проверка наличия штрихов внутри области
            if (HasStrikesInside(binary, largestWhiteRegion))
            {
                double density = CalculateTransitionDensity(binary, largestWhiteRegion);
                if (density > 0.1) // Порог плотности переходов (настраиваемый)
                {
                    Rectangle roi = InflateRectangle(largestWhiteRegion, 10, _bitmap.Width, _bitmap.Height);
                    SaveContoursImage("4_roi_combined.png", new List<Rectangle> { roi });
                    return roi;
                }
            }

            return Rectangle.Empty;
        }

        // Метод для получения инвертированного бинарного изображения
        private int[,] GetInvertedBinaryImage()
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            int[,] grayscale = ToGrayscale();
            int[,] filtered = ApplyMedianFilter(grayscale, 5);
            int[,] binary = new int[width, height];

            double threshold = CalculateAdaptiveThreshold(filtered);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    binary[x, y] = filtered[x, y] > threshold ? 1 : 0; // Фон = 1, штрихи = 0
                }
            }

            return binary;
        }

        // Поиск наибольшей белой области
        private Rectangle FindLargestWhiteRegion(int[,] binary)
        {
            int width = binary.GetLength(0);
            int height = binary.GetLength(1);
            bool[,] visited = new bool[width, height];
            Rectangle largestRect = Rectangle.Empty;
            int maxArea = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (binary[x, y] == 1 && !visited[x, y])
                    {
                        int minX = x, maxX = x, minY = y, maxY = y;
                        FloodFill(binary, visited, x, y, ref minX, ref maxX, ref minY, ref maxY);

                        int rectWidth = maxX - minX + 1;
                        int rectHeight = maxY - minY + 1;
                        int area = rectWidth * rectHeight;
                        float aspectRatio = (float)rectWidth / rectHeight;

                        // Проверяем площадь и соотношение сторон
                        if (area > maxArea &&
                            area < width * height * 0.75 && // Ограничение размера
                            aspectRatio > 1.5f && aspectRatio < 10.0f) // Штрих-код обычно шире, чем выше
                        {
                            maxArea = area;
                            largestRect = new Rectangle(minX, minY, rectWidth, rectHeight);
                        }
                    }
                }
            }

            return largestRect;
        }

        // Заливка для поиска связанных областей
        private void FloodFill(int[,] binary, bool[,] visited, int x, int y,
                               ref int minX, ref int maxX, ref int minY, ref int maxY)
        {
            int width = binary.GetLength(0);
            int height = binary.GetLength(1);
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
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                            binary[nx, ny] == 1 && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }
        }

        // Проверка наличия штрихов внутри области
        private bool HasStrikesInside(int[,] originalBinary, Rectangle region)
        {
            if (region.Top < 0 || region.Top >= _bitmap.Height || region.Left < 0 || region.Right > _bitmap.Width)
                return false;

            int strikeCount = 0;
            int scanLineY = region.Top + region.Height / 2;
            if (scanLineY >= _bitmap.Height) scanLineY = _bitmap.Height - 1;

            for (int x = region.Left; x < region.Right && x < _bitmap.Width; x++)
            {
                if (originalBinary[x, scanLineY] == 0)
                {
                    strikeCount++;
                    x += 3; // Пропуск для избежания подсчёта одного штриха несколько раз
                }
            }

            return strikeCount >= 5;
        }

        // Расширение прямоугольника с учётом границ
        private Rectangle InflateRectangle(Rectangle rect, int padding, int maxWidth, int maxHeight)
        {
            return new Rectangle(
                Math.Max(0, rect.Left - padding),
                Math.Max(0, rect.Top - padding),
                Math.Min(maxWidth - rect.Left, rect.Width + 2 * padding),
                Math.Min(maxHeight - rect.Top, rect.Height + 2 * padding)
            );
        }

        // Основной метод (на базе Собеля)
        public Rectangle DetectBarcodeRegion()
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;

            // Шаг 1: Преобразование в градации серого
            int[,] grayscale = ToGrayscale();
            SaveGrayscale("1_grayscale_sobel.png");

            // Шаг 2: Применение медианного фильтра
            grayscale = ApplyMedianFilter(grayscale, 5);

            // Шаг 3: Вычисление градиентов с помощью фильтра Собеля
            var (magnitude, direction) = ApplySobelFilter(grayscale);
            SaveGradientImage("2_gradient_sobel.png", magnitude);

            // Шаг 4: Сглаживание градиентов
            magnitude = ApplyBlur(magnitude, 5);

            // Шаг 5: Бинаризация с адаптивным порогом
            double threshold = CalculateAdaptiveThreshold(magnitude);
            int[,] binary = ApplyThreshold(magnitude, threshold);
            SaveBinary("3_binary_before_closing_sobel.png", binary);

            // Шаг 6: Морфологическое закрытие
            binary = ApplyMorphologicalClosing(binary, 5);
            SaveBinary("4_binary_after_closing_sobel.png", binary);

            // Шаг 7: Поиск контуров
            List<Rectangle> contours = FindContours(binary);
            SaveContoursImage("5_contours_sobel.png", contours);

            // Шаг 8: Выбор области штрих-кода
            if (contours.Count == 0)
                return Rectangle.Empty;

            Rectangle roi = contours[0];
            roi = InflateRectangle(roi, 5, width, height);
            SaveContoursImage("6_roi_sobel.png", new List<Rectangle> { roi });

            return roi;
        }

        // Комбинированный метод (основной для вызова)
        public Rectangle DetectBarcodeRegionHybrid()
        {
            // Сначала пробуем комбинированный подход (лучше для белого фона)
            Rectangle region = DetectBarcodeRegionCombined();
            if (region != Rectangle.Empty)
                return region;

            // Если не удалось, пробуем подход на базе Собеля
            return DetectBarcodeRegion();
        }

        // Получение профиля из региона
        public int[] GetProfileFromRegion(Rectangle region)
        {
            int[] profile = new int[region.Width];
            int[,] grayscale = ToGrayscale();
            int[,] filtered = ApplyMedianFilter(grayscale, 5);
            double threshold = CalculateAdaptiveThreshold(filtered);

            for (int x = 0; x < region.Width; x++)
            {
                int sum = 0;
                int count = 0;
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    int adjustedX = region.Left + x;
                    if (adjustedX >= 0 && adjustedX < _bitmap.Width && y >= 0 && y < _bitmap.Height)
                    {
                        sum += filtered[adjustedX, y];
                        count++;
                    }
                }
                int avg = count > 0 ? sum / count : 0;
                profile[x] = avg < threshold ? 1 : 0; // Штрихи = 1, фон = 0
            }
            return profile;
        }

        // Диагностика: сохранение градаций серого
        public void SaveGrayscale(string path)
        {
            int[,] grayscale = ToGrayscale();
            Bitmap bmp = new Bitmap(_bitmap.Width, _bitmap.Height);
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    int g = grayscale[x, y];
                    bmp.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            bmp.Save(path);
        }

        // Диагностика: сохранение бинарного изображения
        private void SaveBinary(string path, int[,] binary)
        {
            Bitmap bmp = new Bitmap(_bitmap.Width, _bitmap.Height);
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    int val = binary[x, y] * 255;
                    bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            bmp.Save(path);
        }

        // Диагностика: сохранение изображения градиентов
        private void SaveGradientImage(string path, double[,] gradients)
        {
            int width = _bitmap.Width;
            int height = _bitmap.Height;
            Bitmap bmp = new Bitmap(width, height);
            double maxGradient = gradients.Cast<double>().Max();
            if (maxGradient == 0) maxGradient = 1;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int val = (int)(gradients[x, y] / maxGradient * 255);
                    bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }
            bmp.Save(path);
        }

        // Диагностика: сохранение контуров
        public void SaveContoursImage(string path, List<Rectangle> contours)
        {
            Bitmap result = new Bitmap(_bitmap);
            using (Graphics g = Graphics.FromImage(result))
            using (Pen pen = new Pen(Color.Lime, 2))
            {
                foreach (var rect in contours)
                    if (!rect.IsEmpty)
                        g.DrawRectangle(pen, rect);
            }
            result.Save(path);
        }

        // Выделение области штрих-кода
        public Bitmap HighlightBarcode()
        {
            Rectangle region = DetectBarcodeRegionHybrid();
            Bitmap highlighted = new Bitmap(_bitmap);
            if (region.IsEmpty) return highlighted;

            using (Graphics g = Graphics.FromImage(highlighted))
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawRectangle(pen, region);
                }
            }
            highlighted.Save("7_highlighted_barcode.png");
            return highlighted;
        }

        // Отрисовка контуров
        public Bitmap DrawContours(List<Rectangle> contours)
        {
            Bitmap result = new Bitmap(_bitmap);
            using (Graphics g = Graphics.FromImage(result))
            using (Pen pen = new Pen(Color.Lime, 2))
            {
                foreach (var rect in contours)
                    if (!rect.IsEmpty)
                        g.DrawRectangle(pen, rect);
            }
            return result;
        }

        private double CalculateTransitionDensity(int[,] binary, Rectangle region)
        {
            if (region.Top < 0 || region.Top >= _bitmap.Height || region.Left < 0 || region.Right > _bitmap.Width)
                return 0.0;

            int scanLines = 3;
            int step = region.Height / (scanLines + 1);
            double totalDensity = 0.0;

            for (int i = 1; i <= scanLines; i++)
            {
                int y = region.Top + step * i;
                if (y >= _bitmap.Height) y = _bitmap.Height - 1;

                int transitions = 0;
                int prevValue = binary[Math.Min(region.Left, _bitmap.Width - 1), y];
                for (int x = region.Left + 1; x < region.Right && x < _bitmap.Width; x++)
                {
                    int currentValue = binary[x, y];
                    if (currentValue != prevValue)
                    {
                        transitions++;
                        prevValue = currentValue;
                    }
                }
                double density = (double)transitions / region.Width;
                totalDensity += density;
            }

            return totalDensity / scanLines;
        }
    }
}