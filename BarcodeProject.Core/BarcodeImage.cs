using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

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

    private int[,] GaussianBlur(int[,] input)
    {
        int width = _image.Width;
        int height = _image.Height;
        int[,] result = new int[width, height];
        float[] kernel = { 1, 4, 6, 4, 1, 4, 16, 24, 16, 4, 6, 24, 36, 24, 6, 4, 16, 24, 16, 4, 1, 4, 6, 4, 1 };
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

    private int[,] SobelHorizontal(int[,] input)
    {
        int width = _image.Width;
        int height = _image.Height;
        int[,] gradX = new int[width, height];

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int gx = -input[x - 1, y - 1] - 2 * input[x - 1, y] - input[x - 1, y + 1] +
                         input[x + 1, y - 1] + 2 * input[x + 1, y] + input[x + 1, y + 1];
                gradX[x, y] = Math.Abs(gx);
                if (gradX[x, y] > 255) gradX[x, y] = 255;
            }
        }

        SaveArrayAsImage(gradX, "3_gradient.png");
        return gradX;
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

    private int[,] AdaptiveThreshold(int[,] input)
    {
        int width = _image.Width;
        int height = _image.Height;
        int[,] result = new int[width, height];

        int otsuThreshold = ComputeOtsuThreshold(input);
        int[,] globalResult = new int[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                globalResult[x, y] = input[x, y] >= otsuThreshold ? 255 : 0;
            }
        }
        SaveArrayAsImage(globalResult, "5_otsu.png");

        int blockSize = Math.Max(11, Math.Min(width, height) / 50);
        if (blockSize % 2 == 0) blockSize++;
        double contrastThreshold = 0.08;
        double scaleFactor = 0.3;
        double transitionThreshold = 0.2;

        for (int y = blockSize / 2; y < height - blockSize / 2; y++)
        {
            for (int x = blockSize / 2; x < width - blockSize / 2; x++)
            {
                double sum = 0;
                double sumSquared = 0;
                int count = 0;
                int transitions = 0;
                int lastValue = -1;

                for (int ky = -blockSize / 2; ky <= blockSize / 2; ky++)
                {
                    for (int kx = -blockSize / 2; kx <= blockSize / 2; kx++)
                    {
                        int value = input[x + kx, y + ky];
                        sum += value;
                        sumSquared += value * value;
                        count++;

                        if (ky == 0 && kx > -blockSize / 2)
                        {
                            int currentBin = value > 128 ? 1 : 0;
                            if (lastValue != -1 && currentBin != lastValue)
                            {
                                transitions++;
                            }
                            lastValue = currentBin;
                        }
                    }
                    lastValue = -1;
                }

                double mean = sum / count;
                double variance = (sumSquared / count) - (mean * mean);
                double stdDev = Math.Sqrt(Math.Max(variance, 0));

                double adaptiveThreshold = mean + (stdDev * scaleFactor);
                bool isOtsuWhite = globalResult[x, y] == 255;
                bool isAdaptiveWhite = input[x, y] > adaptiveThreshold &&
                                      stdDev >= contrastThreshold * 255 &&
                                      transitions >= blockSize * transitionThreshold;

                result[x, y] = (isOtsuWhite || isAdaptiveWhite) ? 255 : 0;
            }
        }

        SaveArrayAsImage(result, "5_threshold.png");
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

    public Rectangle DetectBarcodeRegion()
    {
        int[,] blurred = GaussianBlur(_grayImage);
        int[,] gradX = SobelHorizontal(blurred);
        int[,] closed = MorphologicalClosing(gradX);
        int[,] thresh = AdaptiveThreshold(closed);
        var contours = FindContours(thresh);

        if (contours.Length == 0)
        {
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
        {
            return new int[0];
        }

        region = Rectangle.Intersect(region, new Rectangle(0, 0, _image.Width, _image.Height));
        if (region.IsEmpty)
        {
            return new int[0];
        }

        int[,] roi = new int[region.Width, region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                roi[x, y] = _grayImage[region.X + x, region.Y + y];
            }
        }
        SaveArrayAsImage(roi, "7_roi.png");

        int[,] clahe = ApplyCLAHE(_grayImage, region);
        SaveRegionAsImage(clahe, region, "8_clahe.png");

        int[,] binary = OtsuThreshold(clahe, region);
        SaveRegionAsImage(binary, region, "9_binary.png");

        int[] profile = new int[region.Width];
        for (int x = 0; x < region.Width; x++)
        {
            int sum = 0;
            for (int y = 0; y < region.Height; y++)
            {
                sum += binary[region.X + x, region.Y + y];
            }
            profile[x] = sum;
        }

        return profile;
    }
}