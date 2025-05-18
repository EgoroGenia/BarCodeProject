using System;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeProject.Core
{
    public static class Utils
    {
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
                double threshold = mean - 0.5 * stdDev; // Увеличен коэффициент для лучшей обработки разрывов
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