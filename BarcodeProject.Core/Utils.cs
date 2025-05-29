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
            // Пропускаем бинаризацию, если профиль уже бинарный
            if (profile.All(x => x == 0 || x == 1)) return profile;

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
                double threshold = mean - 0.5 * stdDev;
                binary[i] = profile[i] <= threshold ? 1 : 0; // 1 = штрих, 0 = пробел
            }

            return binary;
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

            // Шаг 1: Удаление начальных и конечных белых участков (пробелов, value == 0)
            var trimmedRle = TrimWhitespace(rle);
            if (trimmedRle.Count == 0) return ("", false);

            // Шаг 2: Поиск стартового узора
            (int startIndex, bool isReversed) = FindStartPattern(trimmedRle, barcodeType);
            if (startIndex < 0)
            {
                return ("", false);
            }

            // Обрезаем RLE до начала стартового узора
            trimmedRle = trimmedRle.Skip(startIndex).ToList();

            // Шаг 3: Оценка moduleSize
            var lengths = trimmedRle.Select(item => item.length).Where(l => l > 0).ToList();
            if (lengths.Count == 0) return ("", false);

            double moduleSize = lengths.Median();
            if (barcodeType == "EAN-13" && trimmedRle.Count >= 3)
            {
                var startGuardRle = trimmedRle.Take(3).ToList(); // "101" = 3 модуля
                double guardModuleSize = startGuardRle.Sum(item => item.length) / 3.0;
                moduleSize = Math.Max(moduleSize, guardModuleSize);
            }
            else if (barcodeType == "Code128" && trimmedRle.Count >= 4)
            {
                var startGuardRle = trimmedRle.Take(4).ToList(); // Code 128 start pattern
                double guardModuleSize = startGuardRle.Sum(item => item.length) / 4.0;
                moduleSize = Math.Max(moduleSize, guardModuleSize);
            }
            if (moduleSize < 1) moduleSize = 1;

            // Шаг 4: Объединяем короткие последовательности
            var cleanedRle = new List<(int value, int length)>();
            int currentValue = trimmedRle[0].value;
            int currentLength = trimmedRle[0].length;
            for (int i = 1; i < trimmedRle.Count; i++)
            {
                if (i > 0 && i < trimmedRle.Count - 1 && trimmedRle[i].length < 0.3 * moduleSize && trimmedRle[i].value != currentValue)
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
            cleanedRle.Add((currentValue, currentLength));

            // Шаг 5: Нормализация и формирование битовой строки
            string bitString = "";
            int totalModules = 0;
            foreach (var item in cleanedRle)
            {
                double normalized = item.length / moduleSize;
                int modules;
                if (normalized >= 0.5 && normalized <= 1.5) modules = 1;
                else if (normalized >= 1.5 && normalized <= 2.5) modules = 2;
                else if (normalized >= 2.5 && normalized <= 3.5) modules = 3;
                else if (normalized >= 3.5 && normalized <= 4.5) modules = 4;
                else modules = (int)Math.Round(normalized);
                if (modules < 1) modules = 1;
                totalModules += modules;
                string value = item.value == 1 ? "1" : "0"; // 1 = штрих (чёрный), 0 = пробел (белый)
                bitString += string.Join("", Enumerable.Repeat(value, modules));
            }

            // Если штрих-код перевёрнут, инвертируем битовую строку
            if (isReversed)
            {
                bitString = new string(bitString.Reverse().ToArray());
            }

            // Шаг 6: Проверка валидности
            bool isValid = barcodeType == "EAN-13" ? totalModules == 95 : totalModules >= 35;
            return (bitString, isValid);
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

        public static int[] FilterNoise(int[] profile, int threshold = 10)
        {
            if (profile == null || profile.Length == 0) return profile;

            int[] filtered = (int[])profile.Clone();
            for (int i = 1; i < profile.Length - 1; i++)
            {
                if (Math.Abs(profile[i] - profile[i - 1]) < threshold &&
                    Math.Abs(profile[i] - profile[i + 1]) < threshold)
                {
                    filtered[i] = (profile[i - 1] + profile[i + 1]) / 2;
                }
            }
            return filtered;
        }
    }
}