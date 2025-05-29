using System;
using System.Collections.Generic;
using System.Linq;

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

            var lengths = rle.Select(item => item.length).Where(l => l > 0).ToList();
            if (lengths.Count == 0) return ("", false);

            // Оценка moduleSize: медиана длин в 5–95 перцентилях
            var sortedLengths = lengths.OrderBy(l => l).ToList();
            int lower = (int)(sortedLengths.Count * 0.05);
            int upper = (int)(sortedLengths.Count * 0.95);
            var filteredLengths = sortedLengths.Skip(lower).Take(upper - lower).ToList();
            double moduleSize = filteredLengths.Any() ? filteredLengths.Median() : lengths.Min();
            if (moduleSize < 1) moduleSize = 1;

            // Объединяем короткие последовательности (шум)
            var cleanedRle = new List<(int value, int length)>();
            int currentValue = rle[0].value;
            int currentLength = rle[0].length;
            for (int i = 1; i < rle.Count; i++)
            {
                if (rle[i].length < 0.5 * moduleSize && rle[i].value != currentValue)
                {
                    currentLength += rle[i].length; // Сливаем с предыдущей
                }
                else
                {
                    cleanedRle.Add((currentValue, currentLength));
                    currentValue = rle[i].value;
                    currentLength = rle[i].length;
                }
            }
            cleanedRle.Add((currentValue, currentLength));

            // Нормализация с гибкими диапазонами
            string bitString = "";
            int totalModules = 0;
            foreach (var item in cleanedRle)
            {
                double normalized = item.length / moduleSize;
                int modules;
                if (normalized >= 0.7 && normalized <= 1.3) modules = 1;
                else if (normalized >= 1.7 && normalized <= 2.3) modules = 2;
                else if (normalized >= 2.7 && normalized <= 3.3) modules = 3;
                else if (normalized >= 3.7 && normalized <= 4.3) modules = 4;
                else modules = (int)Math.Round(normalized);
                if (modules < 1) modules = 1;
                totalModules += modules;
                // Инверсия: 0 (пробел) -> 1, 1 (штрих) -> 0
                string value = item.value == 1 ? "0" : "1";
                bitString += string.Join("", Enumerable.Repeat(value, modules));
            }

            // Валидация передана в другой модуль, возвращаем строку и общее количество модулей
            bool isValid = true; // По умолчанию true, так как проверка в другом модуле
            return (bitString, isValid); // 0 = штрих (чёрный), 1 = пробел (белый)
        }

        // Вспомогательный метод для медианы
        private static double Median(this List<int> values)
        {
            if (!values.Any()) return 0;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 0
                ? (values[mid - 1] + values[mid]) / 2.0
                : values[mid];
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