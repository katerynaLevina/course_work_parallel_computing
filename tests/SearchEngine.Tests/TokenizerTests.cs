using SearchEngine.Text;

namespace SearchEngine.Tests;

public class TokenizerTests
{
    [Fact]
    public void Splits_lowercases_and_drops_stopwords() =>
        Assert.Equal(new[] { "quick", "brown", "fox", "jumps" }, Tokenizer.Tokenize("The QUICK brown-fox, jumps!"));

    [Fact]
    public void Keeps_duplicates_and_digits() =>
        Assert.Equal(new[] { "ocean", "2024", "ocean" }, Tokenizer.Tokenize("ocean 2024 ocean"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!.,")]
    [InlineData("the of and")]
    public void Returns_empty_for_no_content(string s) => Assert.Empty(Tokenizer.Tokenize(s));

    [Fact]
    public void Handles_non_ascii_letters() =>
        Assert.Equal(new[] { "café", "naïve" }, Tokenizer.Tokenize("Café naïve"));

    [Fact]
    public void Drops_tokens_shorter_than_minimum_length() =>
        Assert.Equal(new[] { "marks", "spot" }, Tokenizer.Tokenize("x marks spot"));
}
