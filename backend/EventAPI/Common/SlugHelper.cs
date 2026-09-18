using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EventAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Common
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return Guid.NewGuid().ToString("N")[..8];

            string str = RemoveAccent(phrase).ToLowerInvariant();

            // Replace invalid characters with hyphens
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

            // Convert multiple spaces/hyphens into single hyphen
            str = Regex.Replace(str, @"[\s-]+", " ").Trim();

            // Cut to max 200 chars to allow suffix room
            if (str.Length > 200)
                str = str[..200].Trim();

            str = Regex.Replace(str, @"\s", "-");

            return string.IsNullOrWhiteSpace(str) ? Guid.NewGuid().ToString("N")[..8] : str;
        }

        public static async Task<string> GenerateUniqueSlugAsync(EventDbContext context, string title, int? currentEventId = null)
        {
            string baseSlug = GenerateSlug(title);
            string candidate = baseSlug;
            int counter = 1;

            while (true)
            {
                var exists = await context.Events
                    .AsNoTracking()
                    .AnyAsync(e => e.Slug == candidate && (!currentEventId.HasValue || e.EventId != currentEventId.Value));

                if (!exists)
                    return candidate;

                counter++;
                candidate = $"{baseSlug}-{counter}";
            }
        }

        private static string RemoveAccent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Handle Vietnamese 'đ' and 'Đ'
            text = text.Replace("đ", "d").Replace("Đ", "D");

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(normalizedString.Length);

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
