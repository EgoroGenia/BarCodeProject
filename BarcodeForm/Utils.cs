using System;
using System.Collections.Generic;

namespace BarcodeDecoderWinForms
{
    public static class Utils
    {
        // Бинаризация профиля изображения
        public static int[] Binarize(int[] profile)
        {
            int threshold = 128;
            for (int i = 0; i < profile.Length; i++)
            {
                profile[i] = profile[i] < threshold ? 0 : 1;
            }
            return profile;
        }

        // Преобразование в RLE (Run Length Encoding)
        public static List<(int value, int length)> RLE(int[] binary)
        {
            var rle = new List<(int value, int length)>();
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
            rle.Add((currentValue, currentLength)); // Добавляем последнюю группу
            return rle;
        }
    }
}
