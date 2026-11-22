namespace SearchEngine.Index;

/// <summary>One document that contains a term, with the term frequency in it.</summary>
public readonly record struct Posting(int DocId, int Tf);
