using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetInventory
{
    public static class StringUtils
    {
        public static string GetRelativeTimeDifference(DateTime date)
        {
            return GetRelativeTimeDifference(date, DateTime.Now);
        }

        public static string GetRelativeTimeDifference(DateTime date1, DateTime date2)
        {
            TimeSpan difference = date2 - date1;

            if (difference.TotalDays >= 1)
            {
                int days = (int)difference.TotalDays;
                return days == 1 ? "1 day ago" : days + " days ago";
            }
            if (difference.TotalHours >= 1)
            {
                int hours = (int)difference.TotalHours;
                return hours == 1 ? "1 hour ago" : hours + " hours ago";
            }
            if (difference.TotalMinutes >= 1)
            {
                int minutes = (int)difference.TotalMinutes;
                return minutes == 1 ? "1 minute ago" : minutes + " minutes ago";
            }

            int seconds = (int)difference.TotalSeconds;
            return seconds == 1 ? "1 second ago" : seconds + " seconds ago";
        }

        public static string EscapeSQL(string input)
        {
            // Pattern to find 'like' clauses
            string pattern = @"(like\s+'[^']*)";
            string escapePattern = @"(like\s+'[^']*')";

            // Replace underscores with escaped underscores inside 'like' clauses
            input = Regex.Replace(input, pattern, m =>
            {
                string likeClause = m.Groups[1].Value;
                likeClause = likeClause.Replace("_", "\\_");
                return likeClause;
            }, RegexOptions.IgnoreCase);

            // Add ESCAPE '\' behind each 'like' clause
            input = Regex.Replace(input, escapePattern, "$1 ESCAPE '\\'", RegexOptions.IgnoreCase);

            return input;

        }

        public static string CamelCaseToWords(string input)
        {
            string pattern = @"(?<=[a-z])(?=[A-Z])|(?<=[0-9])(?=[A-Z])|(?<=[A-Z])(?=[0-9])|(?<=[0-9])(?=[a-z])";
            string result = Regex.Replace(input, pattern, " ");

            // Further refinement to handle cases with consecutive uppercase letters properly:
            // Ensure space before the start of a new word starting with an uppercase letter followed by lowercase letters
            result = Regex.Replace(result, @"(?<= [A-Z])(?=[A-Z][a-z])", " ");

            // Handle special characters (parentheses)
            result = Regex.Replace(result, @"(?<=[^\s])(?=[(])|(?<=[)])(?=[^\s])", " ");

            // Split the result into words
            string[] words = result.Split(' ');

            // Capitalize the first letter of each word, but keep acronyms in upper case
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CapitalizeFirstLetter(words[i]);
            }

            return string.Join(" ", words);
        }

        private static string CapitalizeFirstLetter(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            // Preserve the case of the rest of the word
            return char.ToUpper(word[0]) + word.Substring(1);
        }

        public static string GetShortHash(string input, int length = 6)
        {
            if (length < 1 || length > 10)
            {
                throw new ArgumentOutOfRangeException(nameof (length), "Length must be between 1 and 10.");
            }

            // Compute a simple hash from the input string.
            int hash = 0;
            foreach (char c in input)
            {
                hash = (hash * 31 + c); // Use a prime number multiplier
            }

            // Calculate the modulus based on the desired length
            int mod = (int)Math.Pow(10, length);

            // Reduce the hash to a number with the desired length
            int shortHash = Math.Abs(hash) % mod;

            // Return the hash as a string, padded with leading zeros if necessary
            return shortHash.ToString($"D{length}");
        }

        public static bool IsUnicode(this string input)
        {
            return input.ToCharArray().Any(c => c > 255);
        }

        public static string StripTags(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public static string StripUnicode(string input)
        {
            return Regex.Replace(input, "&#.*?;", string.Empty);
        }

        public static string ToLabel(string input)
        {
            string result = input;

            // Normalize line breaks to \n
            result = Regex.Replace(result, @"\r\n?|\n", "\n");

            // Translate some HTML tags
            result = result.Replace("<br>", "\n");
            result = result.Replace("</br>", "\n");
            result = result.Replace("<p>", "\n\n");
            result = result.Replace("<p >", "\n\n");
            result = result.Replace("<li>", "\n* ");
            result = result.Replace("<li >", "\n* ");
            result = result.Replace("&nbsp;", " ");
            result = result.Replace("&amp;", "&");

            // Remove remaining tags and also unicode tags
            result = StripUnicode(StripTags(result));

            // Remove whitespace from empty lines
            result = Regex.Replace(result, @"[ \t]+\n", "\n");

            // Ensure at max two consecutive line breaks
            result = Regex.Replace(result, @"\n{3,}", "\n\n");

            return result.Trim();
        }

        public static string ToLowercaseFirstLetter(this string input)
        {
            if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
            {
                return input;
            }

            return char.ToLower(input[0]) + input.Substring(1);
        }

        public static void Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }

        public static string GetEnvVar(string key)
        {
            string value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);

            return value;
        }
    }
}
