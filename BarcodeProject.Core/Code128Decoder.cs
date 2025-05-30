﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BarcodeProject.Core
{
    public static class Code128Decoder
    {
        private static readonly Dictionary<string, int> Code128Patterns = new Dictionary<string, int>
        {
            { "11010010000", 103 }, { "11010011000", 104 }, { "11010011100", 105 },
            { "1100011101011", 106 },
            { "11011001100", 0 }, { "11001101100", 1 }, { "11001100110", 2 }, { "10011101100", 3 },
            { "10011100110", 4 }, { "11001110010", 5 }, { "11001001100", 6 }, { "11001000110", 7 },
            { "11000110100", 8 }, { "10100011000", 9 }, { "10001011000", 10 }, { "10001000110", 11 },
            { "10110001000", 12 }, { "10001101000", 13 }, { "10001100100", 14 }, { "11010001000", 15 },
            { "11000101000", 16 }, { "11000100100", 17 }, { "10110111000", 18 }, { "10110001100", 19 },
            { "10001101100", 20 }, { "10111011000", 21 }, { "10111000110", 22 }, { "10001110110", 23 },
            { "11101101110", 24 }, { "11010001110", 25 }, { "11000101110", 26 }, { "11011101000", 27 },
            { "11011100010", 28 }, { "11011100100", 29 }, { "11110111010", 30 }, { "11001110110", 31 },
            { "11101110100", 32 }, { "11101011000", 33 }, { "11101000110", 34 }, { "11100101100", 35 },
            { "11100100110", 36 }, { "11100110010", 37 }, { "11100010110", 38 }, { "11100011010", 39 },
            { "11101111000", 40 }, { "11001000010", 41 }, { "11110001000", 42 }, { "10100110000", 43 },
            { "10100001100", 44 }, { "10010110000", 45 }, { "10010000110", 46 }, { "10000101100", 47 },
            { "10000100110", 48 }, { "10110010000", 49 }, { "10110000100", 50 }, { "10011010000", 51 },
            { "10011000010", 52 }, { "10000110100", 53 }, { "10000110010", 54 }, { "11000010010", 55 },
            { "11001010000", 56 }, { "11110111000", 57 }, { "11000010100", 58 }, { "10001111010", 59 },
            { "10100111100", 60 }, { "10010111100", 61 }, { "10010011110", 62 }, { "10111100100", 63 },
            { "10011110100", 64 }, { "10011110010", 65 }, { "11110100100", 66 }, { "11110010100", 67 },
            { "11110010010", 68 }, { "11011011110", 69 }, { "11011110110", 70 }, { "11110110110", 71 },
            { "10101111000", 72 }, { "10100101110", 73 }, { "10111101000", 74 }, { "10111100010", 75 },
            { "11110101000", 76 }, { "11110100010", 77 }, { "10111011110", 78 }, { "10111110110", 79 },
            { "11101110110", 80 }, { "11101011110", 81 }, { "11110101110", 82 }, { "11100010100", 83 },
            { "11101110010", 84 }, { "11101111010", 85 }, { "11010000100", 86 }, { "11010010010", 87 },
            { "11010011110", 88 }, { "11000100010", 89 }, { "11010111000", 90 }, { "11000111010", 91 },
            { "11010111100", 92 }, { "11000110000", 93 }, { "11010110000", 94 }, { "11011011000", 95 },
            { "11011000110", 96 }, { "11000110110", 97 }, { "10110011000", 98 }, { "11001100010", 99 },
            { "11001011100", 100 }, { "11001011010", 101 }, { "11001111010", 102 }
        };

        private static readonly Dictionary<int, char> CodeAValues = new Dictionary<int, char>
        {
            { 0, ' ' }, { 1, '!' }, { 2, '"' }, { 3, '#' }, { 4, '$' }, { 5, '%' }, { 6, '&' },
            { 7, '\'' }, { 8, '(' }, { 9, ')' }, { 10, '*' }, { 11, '+' }, { 12, ',' }, { 13, '-' },
            { 14, '.' }, { 15, '/' }, { 16, '0' }, { 17, '1' }, { 18, '2' }, { 19, '3' }, { 20, '4' },
            { 21, '5' }, { 22, '6' }, { 23, '7' }, { 24, '8' }, { 25, '9' }, { 26, ':' }, { 27, ';' },
            { 28, '<' }, { 29, '=' }, { 30, '>' }, { 31, '?' }, { 32, '@' }, { 33, 'A' }, { 34, 'B' },
            { 35, 'C' }, { 36, 'D' }, { 37, 'E' }, { 38, 'F' }, { 39, 'G' }, { 40, 'H' }, { 41, 'I' },
            { 42, 'J' }, { 43, 'K' }, { 44, 'L' }, { 45, 'M' }, { 46, 'N' }, { 47, 'O' }, { 48, 'P' },
            { 49, 'Q' }, { 50, 'R' }, { 51, 'S' }, { 52, 'T' }, { 53, 'U' }, { 54, 'V' }, { 55, 'W' },
            { 56, 'X' }, { 57, 'Y' }, { 58, 'Z' }, { 59, '[' }, { 60, '\\' }, { 61, ']' }, { 62, '^' },
            { 63, '_' }
        };

        private static readonly Dictionary<int, char> CodeBValues = new Dictionary<int, char>
        {
            { 0, ' ' }, { 1, '!' }, { 2, '"' }, { 3, '#' }, { 4, '$' }, { 5, '%' }, { 6, '&' },
            { 7, '\'' }, { 8, '(' }, { 9, ')' }, { 10, '*' }, { 11, '+' }, { 12, ',' }, { 13, '-' },
            { 14, '.' }, { 15, '/' }, { 16, '0' }, { 17, '1' }, { 18, '2' }, { 19, '3' }, { 20, '4' },
            { 21, '5' }, { 22, '6' }, { 23, '7' }, { 24, '8' }, { 25, '9' }, { 26, ':' }, { 27, ';' },
            { 28, '<' }, { 29, '=' }, { 30, '>' }, { 31, '?' }, { 32, '@' }, { 33, 'A' }, { 34, 'B' },
            { 35, 'C' }, { 36, 'D' }, { 37, 'E' }, { 38, 'F' }, { 39, 'G' }, { 40, 'H' }, { 41, 'I' },
            { 42, 'J' }, { 43, 'K' }, { 44, 'L' }, { 45, 'M' }, { 46, 'N' }, { 47, 'O' }, { 48, 'P' },
            { 49, 'Q' }, { 50, 'R' }, { 51, 'S' }, { 52, 'T' }, { 53, 'U' }, { 54, 'V' }, { 55, 'W' },
            { 56, 'X' }, { 57, 'Y' }, { 58, 'Z' }, { 59, '[' }, { 60, '\\' }, { 61, ']' }, { 62, '^' },
            { 63, '_' }, { 64, '`' }, { 65, 'a' }, { 66, 'b' }, { 67, 'c' }, { 68, 'd' }, { 69, 'e' },
            { 70, 'f' }, { 71, 'g' }, { 72, 'h' }, { 73, 'i' }, { 74, 'j' }, { 75, 'k' }, { 76, 'l' },
            { 77, 'm' }, { 78, 'n' }, { 79, 'o' }, { 80, 'p' }, { 81, 'q' }, { 82, 'r' }, { 83, 's' },
            { 84, 't' }, { 85, 'u' }, { 86, 'v' }, { 87, 'w' }, { 88, 'x' }, { 89, 'y' }, { 90, 'z' },
            { 91, '{' }, { 92, '|' }, { 93, '}' }, { 94, '~' }
        };

        private static readonly Dictionary<int, string> CodeCValues = new Dictionary<int, string>
        {
            { 0, "00" }, { 1, "01" }, { 2, "02" }, { 3, "03" }, { 4, "04" }, { 5, "05" },
            { 6, "06" }, { 7, "07" }, { 8, "08" }, { 9, "09" }, { 10, "10" }, { 11, "11" },
            { 12, "12" }, { 13, "13" }, { 14, "14" }, { 15, "15" }, { 16, "16" }, { 17, "17" },
            { 18, "18" }, { 19, "19" }, { 20, "20" }, { 21, "21" }, { 22, "22" }, { 23, "23" },
            { 24, "24" }, { 25, "25" }, { 26, "26" }, { 27, "27" }, { 28, "28" }, { 29, "29" },
            { 30, "30" }, { 31, "31" }, { 32, "32" }, { 33, "33" }, { 34, "34" }, { 35, "35" },
            { 36, "36" }, { 37, "37" }, { 38, "38" }, { 39, "39" }, { 40, "40" }, { 41, "41" },
            { 42, "42" }, { 43, "43" }, { 44, "44" }, { 45, "45" }, { 46, "46" }, { 47, "47" },
            { 48, "48" }, { 49, "49" }, { 50, "50" }, { 51, "51" }, { 52, "52" }, { 53, "53" },
            { 54, "54" }, { 55, "55" }, { 56, "56" }, { 57, "57" }, { 58, "58" }, { 59, "59" },
            { 60, "60" }, { 61, "61" }, { 62, "62" }, { 63, "63" }, { 64, "64" }, { 65, "65" },
            { 66, "66" }, { 67, "67" }, { 68, "68" }, { 69, "69" }, { 70, "70" }, { 71, "71" },
            { 72, "72" }, { 73, "73" }, { 74, "74" }, { 75, "75" }, { 76, "76" }, { 77, "77" },
            { 78, "78" }, { 79, "79" }, { 80, "80" }, { 81, "81" }, { 82, "82" }, { 83, "83" },
            { 84, "84" }, { 85, "85" }, { 86, "86" }, { 87, "87" }, { 88, "88" }, { 89, "89" },
            { 90, "90" }, { 91, "91" }, { 92, "92" }, { 93, "93" }, { 94, "94" }, { 95, "95" },
            { 96, "96" }, { 97, "97" }, { 98, "98" }, { 99, "99" }
        };

        public static string Decode(int[] profile)
        {
            var binary = Utils.Binarize(profile);
            var rle = Utils.RLE(binary);
            var (bitString, isValid) = Utils.ConvertToBitString(rle);

            if (!isValid)
                return "Ошибка: некорректная битовая строка.";

            List<string> chunks = new List<string>();
            int i = 0;
            while (i < bitString.Length)
            {
                int chunkLength = (i + 13 <= bitString.Length && bitString.Substring(i, 13) == "1100011101011") ? 13 : 11;
                if (i + chunkLength <= bitString.Length)
                {
                    chunks.Add(bitString.Substring(i, chunkLength));
                    i += chunkLength;
                }
                else
                {
                    break;
                }
            }

            if (chunks.Count < 3)
                return "Ошибка: недостаточно данных.";

            if (!Code128Patterns.ContainsKey(chunks[0]))
                return "Ошибка: неверный стартовый символ.";

            int startVal = Code128Patterns[chunks[0]];
            string mode;
            switch (startVal)
            {
                case 103: mode = "A"; break;
                case 104: mode = "B"; break;
                case 105: mode = "C"; break;
                default: return "Ошибка: неверный стартовый символ.";
            }

            if (chunks[chunks.Count - 1] != "1100011101011" || Code128Patterns[chunks[chunks.Count - 1]] != 106)
                return "Ошибка: неверный стоповый символ.";

            int checksum = startVal;
            List<int> dataValues = new List<int>();
            for (i = 1; i < chunks.Count - 2; i++)
            {
                string chunk = chunks[i];
                string correctedChunk = DecodePattern(chunk, Code128Patterns.Keys);
                if (correctedChunk == null)
                {
                    return $"Ошибка: неверный символ на позиции {i}.";
                }
                int val = Code128Patterns[correctedChunk];
                checksum += val * i;
                dataValues.Add(val);
            }

            int checkCode = Code128Patterns[chunks[chunks.Count - 2]];
            if ((checksum % 103) != checkCode)
            {
                // Попытка исправить одну цифру
                for (int j = 0; j < dataValues.Count; j++)
                {
                    int originalVal = dataValues[j];
                    for (int newVal = 0; newVal <= 102; newVal++)
                    {
                        if (newVal == originalVal) continue;
                        int tempChecksum = checksum - originalVal * (j + 1) + newVal * (j + 1);
                        if ((tempChecksum % 103) == checkCode)
                        {
                            dataValues[j] = newVal;
                            break;
                        }
                    }
                }
                if ((checksum % 103) != checkCode)
                    return "Ошибка: неверная контрольная сумма.";
            }

            StringBuilder result = new StringBuilder();
            string currentMode = mode;
            foreach (int val in dataValues)
            {
                if (val == 99)
                {
                    currentMode = "C";
                    continue;
                }
                if (val == 100)
                {
                    currentMode = "B";
                    continue;
                }
                if (val == 101)
                {
                    currentMode = "A";
                    continue;
                }

                if (currentMode == "C")
                {
                    if (CodeCValues.ContainsKey(val))
                        result.Append(CodeCValues[val]);
                    else
                        return $"Ошибка: неверное значение {val} в режиме C.";
                }
                else if (currentMode == "B")
                {
                    if (CodeBValues.ContainsKey(val))
                        result.Append(CodeBValues[val]);
                    else
                        return $"Ошибка: неверное значение {val} в режиме B.";
                }
                else if (currentMode == "A")
                {
                    if (CodeAValues.ContainsKey(val))
                        result.Append(CodeAValues[val]);
                    else
                        return $"Ошибка: неверное значение {val} в режиме A.";
                }
            }

            return result.ToString();
        }

        private static string DecodePattern(string chunk, IEnumerable<string> patterns)
        {
            if (Code128Patterns.ContainsKey(chunk))
                return chunk;

            return FindClosestPattern(chunk, patterns);
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
    }
}