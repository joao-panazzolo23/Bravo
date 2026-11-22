namespace Bravo.Application.Tse.Models;

/// <summary>
/// One batch of accumulated results handed from the parser to persistence.
/// </summary>
public sealed record TseResultBatch(Dictionary<TseResultKey, long> Values)
{
    public int Count => Values.Count;
}
