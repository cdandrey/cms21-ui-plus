using System.Globalization;

namespace Cms21UiPlus
{
    public static class TextFormatting
    {
        private static readonly TextInfo EnglishTextInfo =
            new CultureInfo("en-US", false).TextInfo;

        public static string ToTitleCase(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return EnglishTextInfo.ToTitleCase(text.ToLowerInvariant());
        }
    }
}
