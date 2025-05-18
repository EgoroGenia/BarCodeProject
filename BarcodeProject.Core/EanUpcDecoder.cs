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

            // Декодирование левой части
            string leftBits = bitString.Substring(3, 42);
            List<string> leftDigits = new List<string>();
            string maskDetected = "";

            for (int i = 0; i < 6; i++)
            {
                string chunk = leftBits.Substring(i * 7, 7);
                string digit = DecodePattern(chunk, APatterns, BPatterns);
                if (digit == null)
                {
                    return $"Ошибка: неверный левый шаблон на позиции {i + 2}.";
                }
                leftDigits.Add(digit);
                maskDetected += APatterns.ContainsKey(chunk) ? "A" : "B";
            }

            // Определение первой цифры по маске
            int firstDigit = Array.IndexOf(Masks, maskDetected);
            if (firstDigit < 0)
                return "Ошибка: неверная маска кодирования.";

            // Декодирование правой части
            string rightBits = bitString.Substring(50, 42);
            List<string> rightDigits = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                string chunk = rightBits.Substring(i * 7, 7);
                string digit = DecodePattern(chunk, CPatterns);
                if (digit == null)
                {
                    return $"Ошибка: неверный правый шаблон на позиции {i + 8}.";
                }
                rightDigits.Add(digit);
            }

            // Сборка кода
            string fullCode = firstDigit.ToString() + string.Join("", leftDigits) + string.Join("", rightDigits);

            // Проверка контрольной суммы с попыткой исправления
            string correctedCode = VerifyAndCorrectChecksum(fullCode);
            if (correctedCode == null)
                return "Ошибка: неверная контрольная сумма.";

            return correctedCode;
        }

        private static string DecodePattern(string chunk, params Dictionary<string, string>[] patterns)
        {
            foreach (var patternDict in patterns)
            {
                if (patternDict.ContainsKey(chunk))
                    return patternDict[chunk];
            }

            string closest = FindClosestPattern(chunk, patterns.SelectMany(p => p.Keys));
            foreach (var patternDict in patterns)
            {
                if (patternDict.ContainsKey(closest))
                    return patternDict[closest];
            }
            return null;
        }

        private static string FindClosestPattern(string input, IEnumerable<string> patterns)
        {
            int minDistance = int.MaxValue;
            string closest = null;

            foreach (var pattern in patterns)
            {
                int distance = LevenshteinDistance(input, pattern);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = pattern;
                }
            }

            return minDistance <= 2 ? closest : null;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (a.Length != b.Length) return int.MaxValue;
            int[,] d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[a.Length, b.Length];
        }

        private static string VerifyAndCorrectChecksum(string code)
        {
            if (code.Length != 13) return null;

            if (VerifyChecksum(code))
                return code;

            // Попробовать исправить одну цифру
            for (int i = 0; i < 12; i++)
            {
                char original = code[i];
                for (char d = '0'; d <= '9'; d++)
                {
                    if (d == original) continue;
                    string testCode = code.Substring(0, i) + d + code.Substring(i + 1);
                    if (VerifyChecksum(testCode))
                        return testCode;
                }
            }

            return null;
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