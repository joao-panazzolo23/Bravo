using System.Globalization;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Models;
using Bravo.Domain.Geography.Entities;

namespace Bravo.IntegrationTests.Importing;

public sealed class TestStateSource : IImportSource<State>
{
    public const string UnitKey = "default";

    private readonly List<State> _records = [];
    private readonly List<ImportRejectedRecord> _rejected = [];
    private int _failureOffset = -1;
    private int _failureOccurrences;
    private Exception? _failure;

    public void Reset()
    {
        _records.Clear();
        _rejected.Clear();
        _failureOffset = -1;
        _failureOccurrences = 0;
        _failure = null;
    }

    public void Add(params State[] states) => _records.AddRange(states);

    public void Reject(params ImportRejectedRecord[] rejected) => _rejected.AddRange(rejected);

    public void FailAt(int offset, Exception exception, int occurrences = 1)
    {
        _failureOffset = offset;
        _failureOccurrences = occurrences;
        _failure = exception;
    }

    public Task<ImportSourceBatch<State>> ReadBatchAsync(
        string? cursor,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var offset = cursor is null ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);

        if (offset == _failureOffset && _failureOccurrences > 0)
        {
            _failureOccurrences--;
            throw _failure!;
        }

        var records = _records.Skip(offset).Take(batchSize).ToArray();
        var nextOffset = offset + records.Length;
        var hasMore = nextOffset < _records.Count;
        var rejected = offset == 0 ? _rejected.ToArray() : [];

        return Task.FromResult(new ImportSourceBatch<State>(
            records,
            rejected,
            hasMore ? nextOffset.ToString(CultureInfo.InvariantCulture) : null,
            hasMore));
    }
}
