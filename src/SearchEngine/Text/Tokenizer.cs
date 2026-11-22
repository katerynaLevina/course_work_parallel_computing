namespace SearchEngine.Text;

public static class Tokenizer
{
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var lower = text.ToLowerInvariant();
        int start = 0;
        for (int i = 0; i <= lower.Length; i++)
        {
            if (i < lower.Length && char.IsLetterOrDigit(lower[i]))
            {
                continue;
            }

            AddIfKept(tokens, lower.AsSpan(start, i - start));
            start = i + 1;
        }

        return tokens;
    }

    private static void AddIfKept(List<string> tokens, ReadOnlySpan<char> span)
    {
        if (span.Length < Defaults.MinTokenLength)
        {
            return;
        }

        var token = span.ToString();
        if (!Stopwords.Contains(token))
        {
            tokens.Add(token);
        }
    }
}
