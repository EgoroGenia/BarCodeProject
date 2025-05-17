namespace BarcodeProject.Core
{
    public static class BarcodeAnalyzer
    {
        // Метод для декодирования штрих-кодов (передаем профиль изображения)
        public static string AnalyzeBarcode(int[] profile, string barcodeType)
        {
            switch (barcodeType)
            {
                case "EAN-13":
                    return EanUpcDecoder.DecodeEan13(profile);
                
                case "Code 128":
                    return Code128Decoder.Decode(profile);
                default:
                    return "Тип штрих-кода не поддерживается.";
            }
        }
    }
}
