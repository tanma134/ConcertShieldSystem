using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EventAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Common
{
    public static class SlugHelper
    {
        private static readonly Regex ValidSlugRegex = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

        public static string GenerateSlug(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return Guid.NewGuid().ToString("N")[..8];

            return NormalizeSlug(phrase);
        }

        public static string NormalizeSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string str = RemoveAccent(text).ToLowerInvariant();

            // Replace invalid characters with hyphens
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

            // Convert multiple spaces/hyphens into single hyphen
            str = Regex.Replace(str, @"[\s-]+", "-").Trim('-');

            // Cut to max 200 chars to allow suffix room
            if (str.Length > 200)
            {
                str = str[..200].TrimEnd('-');
            }

            return str;
        }

        public static bool IsValidSlug(string? slug, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                errorMessage = "Slug is required.";
                return false;
            }

            var trimmed = slug.Trim();
            if (trimmed.Length < 3)
            {
                errorMessage = "Slug must be at least 3 characters.";
                return false;
            }

            if (trimmed.Length > 200)
            {
                errorMessage = "Slug must not exceed 200 characters.";
                return false;
            }

            if (trimmed.StartsWith('-') || trimmed.EndsWith('-'))
            {
                errorMessage = "Slug cannot start or end with a hyphen.";
                return false;
            }

            if (trimmed.Contains("--"))
            {
                errorMessage = "Slug cannot contain consecutive hyphens.";
                return false;
            }

            if (!ValidSlugRegex.IsMatch(trimmed))
            {
                errorMessage = "Slug can only contain lowercase letters (a-z), numbers (0-9), and hyphens (-).";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public static async Task<string> GenerateUniqueSlugAsync(EventDbContext context, string title, int? currentEventId = null)
        {
            string baseSlug = GenerateSlug(title);
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = Guid.NewGuid().ToString("N")[..8];

            string candidate = baseSlug;
            int counter = 1;

            while (true)
            {
                var exists = await context.Events
                    .AsNoTracking()
                    .AnyAsync(e => e.Slug == candidate && !e.IsDeleted && (!currentEventId.HasValue || e.EventId != currentEventId.Value));

                if (!exists)
                    return candidate;

                counter++;
                candidate = $"{baseSlug}-{counter}";
            }
        }

        public static async Task<(bool Available, string Suggestion)> CheckAvailabilityAsync(EventDbContext context, string rawSlug, int? excludeEventId = null)
        {
            string normalized = NormalizeSlug(rawSlug);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return (false, string.Empty);
            }

            var exists = await context.Events
                .AsNoTracking()
                .AnyAsync(e => e.Slug == normalized && !e.IsDeleted && (!excludeEventId.HasValue || e.EventId != excludeEventId.Value));

            if (!exists)
            {
                return (true, normalized);
            }

            // Find next available suggestion
            int counter = 2;
            string suggestion = $"{normalized}-{counter}";
            while (await context.Events.AsNoTracking().AnyAsync(e => e.Slug == suggestion && !e.IsDeleted && (!excludeEventId.HasValue || e.EventId != excludeEventId.Value)))
            {
                counter++;
                suggestion = $"{normalized}-{counter}";
            }

            return (false, suggestion);
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
