using System;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeDecoderWinForms
{
    public static class EanUpcDecoder
    {
        private static readonly Dictionary<string, string> Ean13Patterns = new Dictionary<string, string>
        {
            { "0001101", "0" },
            { "0011001", "1" },
            { "0010011", "2" },
            { "0111101", "3" },
            { "0100011", "4" },
            { "0110001", "5" },
            { "0101011", "6" },
            { "0110111", "7" },
            { "0001011", "8" },
            { "0010111", "9" }
        };

        public static string DecodeEan13(int[] profile)
        {
            var binary = Utils.Binarize(profile);
            var rle = Utils.RLE(binary);

            string result = "";
            for (int i = 0; i < 13; i++)
            {
                string code = GetCode(rle, i);
                if (Ean13Patterns.ContainsKey(code))
                {
                    result += Ean13Patterns[code];
                }
                else
                {
                    result += "?"; // Если код не найден, возвращаем знак вопроса
                }
            }

            return result;
        }

        private static string GetCode(List<(int value, int length)> rle, int index)
        {
            string code = "";
            int bitCount = 0;
            int rleIndex = 0;

            // Пропускаем предыдущие символы
            while (rleIndex < rle.Count && bitCount < index * 7) // Каждый символ EAN-13 занимает 7 бит
            {
                bitCount += rle[rleIndex].length;
                rleIndex++;
            }

            // Собираем 7 бит для текущего символа
            while (rleIndex < rle.Count && code.Length < 7)
            {
                var (value, length) = rle[rleIndex];
                code += string.Join("", Enumerable.Repeat(value.ToString(), Math.Min(length, 7 - code.Length)));
                rleIndex++;
            }

            return code.Length == 7 ? code : ""; // Возвращаем только полные 7-битные коды
        }

        public static string DecodeUpcA(int[] profile)
        {
            // Логика для декодирования UPC-A будет аналогична EAN-13, с учетом отличий
            return DecodeEan13(profile); // Пока используем ту же логику
        }


    }
}
