using System;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeProject.Core
{
    public static class Utils
    {
        public static int[] AdaptiveBinarize(int[] profile)
        {
            if (profile == null || profile.Length == 0)
                return new int[0];

            // Адаптивная бинаризация с плавающим порогом
            int windowSize = Math.Max(15, profile.Length / 30);
            if (windowSize % 2 == 0) windowSize++;

            int[] binary = new int[profile.Length];
            double[] smoothed = new double[profile.Length];

            // Сначала сглаживаем профиль
            for (int i = 0; i < profile.Length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(profile.Length, i + windowSize / 2 + 1);
                double sum = 0;

                for (int j = start; j < end; j++)
                {
                    sum += profile[j];
                }
                smoothed[i] = sum / (end - start);
            }

            // Затем бинаризуем с адаптивным порогом
            for (int i = 0; i < profile.Length; i++)
            {
                double threshold = smoothed[i] * 0.7; // Коэффициент можно настроить
                binary[i] = (profile[i] < threshold) ? 1 : 0;
            }

            return binary;
        }

        public static int[] CleanBinaryProfile(int[] binary)
        {
            // Удаляем одиночные пиксели (шум)
            int minBarWidth = 2; // Минимальная ширина полосы
            int[] cleaned = new int[binary.Length];
            Array.Copy(binary, cleaned, binary.Length);

            for (int i = 1; i < binary.Length - 1; i++)
            {
                if (binary[i] != binary[i - 1] && binary[i] != binary[i + 1])
                {
                    cleaned[i] = binary[i - 1]; // Исправляем одиночный выброс
                }
            }

            // Объединяем очень близкие полосы
            for (int i = 1; i < cleaned.Length; i++)
            {
                if (cleaned[i] != cleaned[i - 1])
                {
                    int j = i;
                    while (j < cleaned.Length && cleaned[j] == cleaned[i] && (j - i) < minBarWidth)
                        j++;

                    if ((j - i) < minBarWidth)
                    {
                        for (int k = i; k < j; k++)
                            cleaned[k] = cleaned[i - 1];
                    }
                }
            }

            return cleaned;
        }

        public static List<(int value, int length)> RunLengthEncode(int[] binary)
        {
            var rle = new List<(int value, int length)>();
            if (binary.Length == 0) return rle;

            int current = binary[0];
            int count = 1;

            for (int i = 1; i < binary.Length; i++)
            {
                if (binary[i] == current)
                {
                    count++;
                }
                else
                {
                    rle.Add((current, count));
                    current = binary[i];
                    count = 1;
                }
            }
            rle.Add((current, count));

            return rle;
        }

        public static List<(int value, int normalizedWidth)> NormalizeModules(List<(int value, int length)> rle)
        {
            if (rle.Count == 0) return new List<(int, int)>();

            // Вычисляем базовую ширину модуля (самую частую ширину)
            var widths = rle.Select(x => x.length).ToList();
            int baseWidth = widths.GroupBy(x => x)
                                 .OrderByDescending(g => g.Count())
                                 .First()
                                 .Key;

            // Нормализуем ширины относительно базовой
            var normalized = new List<(int, int)>();
            foreach (var item in rle)
            {
                double ratio = (double)item.length / baseWidth;
                int normalizedWidth = (int)Math.Round(ratio);
                if (normalizedWidth < 1) normalizedWidth = 1;
                normalized.Add((item.value, normalizedWidth));
            }

            return normalized;
        }

        // Сохранение исходных методов для совместимости
        public static int[] Binarize(int[] profile)
        {
            if (profile.Length == 0) return new int[0];

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
                binary[i] = profile[i] <= threshold ? 1 : 0;
            }

            return binary;
        }

        public static List<(int value, int length)> RLE(int[] binary)
        {
            var rle = new List<(int value, int length)>();
            if (binary.Length == 0) return rle;

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

        public static (string bitString, bool isValid) ConvertToBitString(List<(int value, int length)> rle)
        {
            var lengths = rle.Select(item => item.length).Where(l => l > 0).ToList();
            if (lengths.Count == 0) return ("", false);

            double moduleSize = lengths.Min();
            if (moduleSize < 1) moduleSize = 1;

            string bitString = "";
            foreach (var item in rle)
            {
                double normalized = item.length / moduleSize;
                int modules;
                if (normalized >= 0.8 && normalized <= 1.2) modules = 1;
                else if (normalized >= 1.8 && normalized <= 2.2) modules = 2;
                else if (normalized >= 2.8 && normalized <= 3.2) modules = 3;
                else if (normalized >= 3.8 && normalized <= 4.2) modules = 4;
                else modules = (int)Math.Round(normalized);
                if (modules < 1) modules = 1;
                bitString += string.Join("", Enumerable.Repeat(item.value.ToString(), modules));
            }

            return (bitString, true);
        }
    }
}