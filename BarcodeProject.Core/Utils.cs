using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BarcodeProject.Core
{
    public static class Utils
    {
        public static int[] Binarize(int[] profile)
        {
            if (profile == null || profile.Length == 0) return new int[0];
            if (profile.All(x => x == 0 || x == 1)) return profile;

            // Глобальный порог по методу Отсу
            double globalThreshold = CalculateOtsuThreshold(profile);

            int windowSize = Math.Max(11, profile.Length / 20);
            if (windowSize % 2 == 0) windowSize++;
            int[] binary = new int[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(profile.Length, i + windowSize / 2 + 1);
                int[] window = new int[end - start];
                for (int j = start, k = 0; j < end; j++, k++)
                {
                    window[k] = profile[j];
                }
                double mean = window.Average();
                double sumSquared = window.Sum(v => (v - mean) * (v - mean));
                double stdDev = Math.Sqrt(sumSquared / window.Length);
                double localThreshold = mean - 0.5 * stdDev;

                // Комбинируем глобальный и локальный порог
                double threshold = (globalThreshold + localThreshold) / 2.0;
                binary[i] = profile[i] <= threshold ? 1 : 0; // 1 = штрих, 0 = пробел
            }

            return binary;
        }

        private static double CalculateOtsuThreshold(int[] profile)
        {
            int[] histogram = new int[256];
            foreach (var value in profile)
            {
                if (value >= 0 && value < 256) histogram[value]++;
            }

            int total = profile.Length;
            double sum = 0;
            for (int i = 0; i < 256; i++)
                sum += i * histogram[i];

            double sumB = 0;
            int wB = 0;
            int wF = 0;
            double maxVariance = 0;
            int threshold = 0;

            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;
                wF = total - wB;
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

        public static List<(int value, int length)> RLE(int[] binary)
        {
            var rle = new List<(int value, int length)>();
            if (binary == null || binary.Length == 0) return rle;

            int currentValue = binary[0];
            int currentLength = 1;

            for (int i = 1; i < binary.Length; i++)
            {
                if (binary[i] == currentValue)
                {
                    currentLength++;
                }
                else
                {
                    rle.Add((currentValue, currentLength));
                    currentValue = binary[i];
                    currentLength = 1;
                }
            }
            rle.Add((currentValue, currentLength));
            return rle;
        }

        public static (string bitString, bool isValid) ConvertToBitString(List<(int value, int length)> rle, string barcodeType = "EAN-13")
        {
            if (rle == null || rle.Count == 0) return ("", false);

            // Шаг 1: Удаление начальных и конечных пробелов
            var trimmedRle = TrimWhitespace(rle);
            if (trimmedRle.Count == 0) return ("", false);

            // Шаг 2: Поиск стартового узора
            (int startIndex, bool isReversed) = FindStartPattern(trimmedRle, barcodeType);
            if (startIndex < 0) return ("", false);

            trimmedRle = trimmedRle.Skip(startIndex).ToList();

            // Шаг 3: Оценка moduleSize
            double moduleSize = EstimateModuleSize(trimmedRle, barcodeType);
            if (moduleSize < 1) moduleSize = 1;

            // Шаг 4: Объединяем короткие последовательности
            var cleanedRle = new List<(int value, int length)>();
            int currentValue = trimmedRle[0].value;
            int currentLength = trimmedRle[0].length;
            double minLengthThreshold = moduleSize * 0.5; // Динамический порог
            for (int i = 1; i < trimmedRle.Count; i++)
            {
                if (i > 0 && i < trimmedRle.Count - 1 &&
                    trimmedRle[i].length < minLengthThreshold &&
                    trimmedRle[i].value != currentValue)
                {
                    if (i > 1 && i < trimmedRle.Count - 2 &&
                        trimmedRle[i - 1].length >= minLengthThreshold &&
                        trimmedRle[i + 1].length >= minLengthThreshold)
                    {
                        currentLength += trimmedRle[i].length;
                    }
                    else
                    {
                        cleanedRle.Add((currentValue, currentLength));
                        currentValue = trimmedRle[i].value;
                        currentLength = trimmedRle[i].length;
                    }
                }
                else
                {
                    cleanedRle.Add((currentValue, currentLength));
                    currentValue = trimmedRle[i].value;
                    currentLength = trimmedRle[i].length;
                }
            }
            cleanedRle.Add((currentValue, currentLength));

            // Шаг 5: Формирование битовой строки
            string bitString = "";
            int totalModules = 0;
            foreach (var item in cleanedRle)
            {
                double normalized = item.length / moduleSize;
                int modules = (int)Math.Round(normalized);
                if (modules < 1) modules = 1;
                totalModules += modules;
                string value = item.value == 1 ? "1" : "0";
                bitString += string.Join("", Enumerable.Repeat(value, modules));
            }

            if (isReversed)
            {
                bitString = new string(bitString.Reverse().ToArray());
            }

            // Шаг 6: Корректировка длины битовой строки
            int expectedLength = barcodeType == "EAN-13" ? 95 : 33; // Минимальная длина для Code 128
            if (bitString.Length != expectedLength && totalModules >= expectedLength * 0.9 && totalModules <= expectedLength * 1.1)
            {
                // Пересчитываем moduleSize для соответствия ожидаемой длине
                moduleSize *= ((double)expectedLength / totalModules);
                bitString = "";
                totalModules = 0;
                cleanedRle.Clear();
                currentValue = trimmedRle[0].value;
                currentLength = trimmedRle[0].length;
                for (int i = 1; i < trimmedRle.Count; i++)
                {
                    if (i > 0 && i < trimmedRle.Count - 1 &&
                        trimmedRle[i].length < minLengthThreshold &&
                        trimmedRle[i].value != currentValue)
                    {
                        if (i > 1 && i < trimmedRle.Count - 2 &&
                            trimmedRle[i - 1].length >= minLengthThreshold &&
                            trimmedRle[i + 1].length >= minLengthThreshold)
                        {
                            currentLength += trimmedRle[i].length;
                        }
                        else
                        {
                            cleanedRle.Add((currentValue, currentLength));
                            currentValue = trimmedRle[i].value;
                            currentLength = trimmedRle[i].length;
                        }
                    }
                    else
                    {
                        cleanedRle.Add((currentValue, currentLength));
                        currentValue = trimmedRle[i].value;
                        currentLength = trimmedRle[i].length;
                    }
                }
                cleanedRle.Add((currentValue, currentLength));

                foreach (var item in cleanedRle)
                {
                    double normalized = item.length / moduleSize;
                    int modules = (int)Math.Round(normalized);
                    if (modules < 1) modules = 1;
                    totalModules += modules;
                    string value = item.value == 1 ? "1" : "0";
                    bitString += string.Join("", Enumerable.Repeat(value, modules));
                }

                if (isReversed)
                {
                    bitString = new string(bitString.Reverse().ToArray());
                }
            }

            // Шаг 7: Проверка валидности
            bool isValid = barcodeType == "EAN-13" ? bitString.Length == 95 : totalModules >= 33;
            return (bitString, isValid);
        }

        private static double EstimateModuleSize(List<(int value, int length)> rle, string barcodeType)
        {
            var lengths = rle.Select(item => item.length).Where(l => l > 0).ToList();
            if (lengths.Count == 0) return 1.0;

            // Начальная оценка на основе стартового узора
            double guardModuleSize = 0;
            if (barcodeType == "EAN-13" && rle.Count >= 3)
            {
                guardModuleSize = rle.Take(3).Sum(item => item.length) / 3.0; // "101" = 3 модуля
            }
            else if (barcodeType == "Code128" && rle.Count >= 4)
            {
                guardModuleSize = rle.Take(4).Sum(item => item.length) / 11.0; // 11 модулей
            }

            // Кластеризация длин
            var sortedLengths = lengths.OrderBy(l => l).ToList();
            var clusters = new List<double>();
            double currentSum = sortedLengths[0];
            int count = 1;
            for (int i = 1; i < sortedLengths.Count; i++)
            {
                if (sortedLengths[i] < currentSum / count * 1.5) // Объединяем близкие длины
                {
                    currentSum += sortedLengths[i];
                    count++;
                }
                else
                {
                    clusters.Add(currentSum / count);
                    currentSum = sortedLengths[i];
                    count = 1;
                }
            }
            clusters.Add(currentSum / count);

            // Выбираем минимальный кластер как ширину одного модуля
            double moduleSize = clusters.Where(c => c > 0).Min();
            if (guardModuleSize > 0)
            {
                moduleSize = (moduleSize + guardModuleSize) / 2.0; // Усредняем с узором
            }

            return Math.Max(1.0, moduleSize);
        }

        public static List<(int value, int length)> TrimWhitespace(List<(int value, int length)> rle)
        {
            if (rle == null || rle.Count == 0) return rle;

            // Удаляем начальные и конечные пробелы (value == 0)
            int start = 0;
            while (start < rle.Count && rle[start].value == 0)
                start++;

            int end = rle.Count - 1;
            while (end >= 0 && rle[end].value == 0)
                end--;

            if (start > end) return new List<(int value, int length)>();
            return rle.Skip(start).Take(end - start + 1).ToList();
        }

        private static (int startIndex, bool isReversed) FindStartPattern(List<(int value, int length)> rle, string barcodeType)
        {
            string[] expectedPatterns = barcodeType == "EAN-13"
                ? new[] { "101" }
                : new[] { "11010010000", "11010011000", "11010011100" }; // Code 128 start patterns

            // Преобразуем RLE в битовую строку для поиска узора
            double moduleSize = rle.Select(item => item.length).Where(l => l > 0).Median();
            if (moduleSize < 1) moduleSize = 1;

            StringBuilder tempBitString = new StringBuilder();
            foreach (var item in rle)
            {
                int modules = (int)Math.Round(item.length / moduleSize);
                if (modules < 1) modules = 1;
                string value = item.value == 1 ? "1" : "0";
                tempBitString.Append(string.Join("", Enumerable.Repeat(value, modules)));
            }

            string bitString = tempBitString.ToString();
            foreach (var pattern in expectedPatterns)
            {
                int index = bitString.IndexOf(pattern);
                if (index >= 0)
                {
                    // Находим индекс в RLE, соответствующий началу узора
                    int totalModules = 0;
                    for (int i = 0; i < rle.Count; i++)
                    {
                        int modules = (int)Math.Round(rle[i].length / moduleSize);
                        if (modules < 1) modules = 1;
                        if (totalModules + modules > index)
                        {
                            return (i, false);
                        }
                        totalModules += modules;
                    }
                    return (0, false);
                }
            }

            // Проверяем инвертированную строку
            string reversedBitString = new string(bitString.Reverse().ToArray());
            foreach (var pattern in expectedPatterns)
            {
                int index = reversedBitString.IndexOf(pattern);
                if (index >= 0)
                {
                    return (0, true); // Перевёрнутый штрих-код, начинаем с начала
                }
            }

            return (-1, false);
        }

        public static double Median(this IEnumerable<int> values)
        {
            if (values == null || !values.Any()) return 0;
            var sortedValues = values.OrderBy(x => x).ToList();
            int mid = sortedValues.Count / 2;
            return sortedValues.Count % 2 == 0
                ? (sortedValues[mid - 1] + sortedValues[mid]) / 2.0
                : sortedValues[mid];
        }

        public static int[] NormalizeProfile(int[] profile)
        {
            if (profile == null || profile.Length == 0) return profile;

            int min = profile.Min();
            int max = profile.Max();
            if (max == min) return profile;

            int[] normalized = new int[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                normalized[i] = (int)((profile[i] - min) * 255.0 / (max - min));
            }
            return normalized;
        }

        public static int[] FilterNoise(int[] profile, int windowSize = 5)
        {
            if (profile == null || profile.Length == 0) return profile;

            int[] filtered = new int[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(profile.Length, i + windowSize / 2 + 1);
                int[] window = new int[end - start];
                for (int j = start, k = 0; j < end; j++, k++)
                {
                    window[k] = profile[j];
                }
                filtered[i] = (int)window.OrderBy(x => x).Median();
            }
            return filtered;
        }
    }
}