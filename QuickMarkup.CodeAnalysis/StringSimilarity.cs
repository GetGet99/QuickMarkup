namespace QuickMarkup.CodeAnalysis;

static class StringSimilarity
{
    public static int LevenshteinDistance(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var lenA = a.Length;
        var lenB = b.Length;

        if (lenA == 0) return lenB;
        if (lenB == 0) return lenA;

        var row = new int[lenB + 1];
        for (var j = 0; j <= lenB; j++)
            row[j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            var prev = row[0];
            row[0] = i;
            for (var j = 1; j <= lenB; j++)
            {
                var temp = row[j];
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                row[j] = Math.Min(Math.Min(row[j - 1] + 1, row[j] + 1), prev + cost);
                prev = temp;
            }
        }

        return row[lenB];
    }

    public static string[]? GetSuggestions(string input, IEnumerable<string> candidates, int maxResults = 3)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        var maxDistance = Math.Min(3, Math.Max(1, input.Length / 2));

        var scored = candidates
            .Select(c => (Name: c, Distance: LevenshteinDistance(input, c)))
            .Where(x => x.Distance <= maxDistance)
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Name)
            .Take(maxResults)
            .ToArray();

        return scored.Length > 0 ? scored.Select(x => x.Name).ToArray() : null;
    }
}
