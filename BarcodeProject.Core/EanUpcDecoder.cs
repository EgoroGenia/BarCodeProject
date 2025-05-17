using System;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeProject.Core
{
    public static class EanUpcDecoder
    {
        private static readonly Dictionary<string, string> APatterns = new Dictionary<string, string>
        {
            { "0001101", "0" }, { "0011001", "1" }, { "0010011", "2" }, { "0111101", "3" },
            { "0100011", "4" }, { "0110001", "5" }, { "0101111", "6" }, { "0111011", "7" },
            { "0110111", "8" }, { "0001011", "9" }
        };

        private static readonly Dictionary<string, string> BPatterns = new Dictionary<string, string>
        {
            { "0100111", "0" }, { "0110011", "1" }, { "0011011", "2" }, { "0100001", "3" },
            { "0011101", "4" }, { "0111001", "5" }, { "0000101", "6" }, { "0010001", "7" },
            { "0001001", "8" }, { "0010111", "9" }
        };

        private static readonly Dictionary<string, string> CPatterns = new Dictionary<string, string>
        {
            { "1110010", "0" }, { "1100110", "1" }, { "1101100", "2" }, { "1000010", "3" },
            { "1011100", "4" }, { "1001110", "5" }, { "1010000", "6" }, { "1000100", "7" },
            { "1001000", "8" }, { "1110100", "9" }
        };

        private static readonly string[] Masks = {
            "AAAAAA", "AABABB", "AABBAB", "AABBBA", "ABAABB",
            "ABBAAB", "ABBBAA", "ABABAB", "ABABBA", "ABBABA"
        };

        private static readonly string StartGuard = "101";
        private static readonly string MiddleGuard = "01010";
        private static readonly string EndGuard = "101";

        public static string DecodeEan13(int[] profile)
        {
            var binary = Utils.Binarize(profile);
            var rle = Utils.RLE(binary);
            var (bitString, isValid) = Utils.ConvertToBitString(rle);

            if (!isValid || bitString.Length < 95)
                return "Ошибка: недостаточная длина штрихкода.";

            // Проверка ориентации
            string reversedBinary = new string(bitString.Reverse().ToArray());
            bool isReversed = false;
            if (!bitString.StartsWith(StartGuard))
            {
                if (reversedBinary.StartsWith(StartGuard))
                {
                    bitString = reversedBinary;
                    isReversed = true;
                }
                else
                {
                    return "Ошибка: неверный стартовый узор.";
                }
            }

            // Проверка центрального и конечного узоров
            if (bitString.Length < 95 || bitString.Substring(45, 5) != MiddleGuard)
                return "Ошибка: неверный центральный узор.";
            if (bitString.Substring(92, 3) != EndGuard)
                return "Ошибка: неверный конечный узор.";

            // Декодирование левой части (42 модуля, цифры 2–7)
            string leftBits = bitString.Substring(3, 42);
            List<string> leftDigits = new List<string>();
            string maskDetected = "";

            for (int i = 0; i < 6; i++)
            {
                string chunk = leftBits.Substring(i * 7, 7);
                if (APatterns.ContainsKey(chunk))
                {
                    leftDigits.Add(APatterns[chunk]);
                    maskDetected += "A";
                }
                else if (BPatterns.ContainsKey(chunk))
                {
                    leftDigits.Add(BPatterns[chunk]);
                    maskDetected += "B";
                }
                else
                {
                    string correctedChunk = FindClosestPattern(chunk, APatterns.Keys.Concat(BPatterns.Keys));
                    if (correctedChunk != null && APatterns.ContainsKey(correctedChunk))
                    {
                        leftDigits.Add(APatterns[correctedChunk]);
                        maskDetected += "A";
                    }
                    else if (correctedChunk != null && BPatterns.ContainsKey(correctedChunk))
                    {
                        leftDigits.Add(BPatterns[correctedChunk]);
                        maskDetected += "B";
                    }
                    else
                    {
                        return $"Ошибка: неверный левый шаблон на позиции {i + 2}.";
                    }
                }
            }

            // Определение первой цифры по маске
            int firstDigit = Array.IndexOf(Masks, maskDetected);
            if (firstDigit < 0)
                return "Ошибка: неверная маска кодирования.";

            // Декодирование правой части (42 модуля, цифры 8–13)
            string rightBits = bitString.Substring(50, 42);
            List<string> rightDigits = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                string chunk = rightBits.Substring(i * 7, 7);
                if (CPatterns.ContainsKey(chunk))
                {
                    rightDigits.Add(CPatterns[chunk]);
                }
                else
                {
                    string correctedChunk = FindClosestPattern(chunk, CPatterns.Keys);
                    if (correctedChunk != null && CPatterns.ContainsKey(correctedChunk))
                    {
                        rightDigits.Add(CPatterns[correctedChunk]);
                    }
                    else
                    {
                        return $"Ошибка: неверный правый шаблон на позиции {i + 8}.";
                    }
                }
            }

            // Сборка кода
            string fullCode = firstDigit.ToString() + string.Join("", leftDigits) + string.Join("", rightDigits);

            // Проверка контрольной цифры
            if (!VerifyChecksum(fullCode))
                return "Ошибка: неверная контрольная сумма.";

            return fullCode;
        }

        public static string DecodeUpcA(int[] profile)
        {
            string ean13 = DecodeEan13(profile);
            if (ean13.StartsWith("0") && ean13.Length == 13)
                return ean13.Substring(1);
            return ean13;
        }

        private static string FindClosestPattern(string input, IEnumerable<string> patterns)
        {
            int minDistance = int.MaxValue;
            string closest = null;

            foreach (var pattern in patterns)
            {
                int distance = HammingDistance(input, pattern);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = pattern;
                }
            }

            return minDistance <= 2 ? closest : null;
        }

        private static int HammingDistance(string a, string b)
        {
            if (a.Length != b.Length) return int.MaxValue;
            return a.Zip(b, (x, y) => x == y ? 0 : 1).Sum();
        }

        private static bool VerifyChecksum(string code)
        {
            if (code.Length != 13) return false;

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit = int.Parse(code[i].ToString());
                sum += (i % 2 == 0) ? digit : digit * 3;
            }
            int checksum = (10 - (sum % 10)) % 10;
            return checksum == int.Parse(code[12].ToString());
        }
    }
}