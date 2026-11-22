using Bravo.Domain.Importing.Enums;
using System.Collections.Concurrent;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Deterministic in-memory import source for the job lifecycle tests. It records how many times
/// it ran and how much parallelism it actually observed, and can fail on demand.
/// </summary>
public sealed class FakeImportSource : IImportSource
{
    public const string DefaultKey = "FAKE";
    public const string DefaultScope = "FAKE:test";

    private readonly ConcurrentQueue<FakeRun> _runs = new();
    private int _attempt;

    public string Key { get; init; } = DefaultKey;

    public int Operations { get; set; } = 4;

    public int RecordsPerOperation { get; set; } = 10;

    /// <summary>Returns an exception for the given attempt number, or null to succeed.</summary>
    public Func<int, Exception?>? Failure { get; set; }

    /// <summary>How long each operation stays active, long enough to overlap.</summary>
    public int OperationDelayMilliseconds { get; set; } = 50;

    public IReadOnlyList<FakeRun> Runs => _runs.ToArray();

    public int ExecutionCount => _runs.Count;

    public void Reset()
    {
        while (_runs.TryDequeue(out _))
        {
        }

        _attempt = 0;
        Failure = null;
        Operations = 4;
        RecordsPerOperation = 10;
        OperationDelayMilliseconds = 50;
    }

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _attempt);
        var run = new FakeRun(attempt);

        _runs.Enqueue(run);

        context.Report(ImportStage.Candidates, Operations, 0, 0, 0, 0, "Starting");

        var failure = Failure?.Invoke(attempt);

        if (failure is not null)
            throw failure;

        var completed = 0;
        long processed = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(1, Operations),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, context.MaxParallelism),
                CancellationToken = cancellationToken
            },
            async (operation, token) =>
            {
                run.Enter();

                try
                {
                    await Task.Delay(OperationDelayMilliseconds, token);
                }
                finally
                {
                    run.Exit();
                }

                Interlocked.Add(ref processed, RecordsPerOperation);
                var finished = Interlocked.Increment(ref completed);

                context.Report(
                    ImportStage.Persisting,
                    Operations,
                    Volatile.Read(ref finished),
                    0,
                    Volatile.Read(ref processed),
                    0,
                    $"Operation {operation}");
            });
    }

    public sealed class FakeRun(int attempt)
    {
        private int _active;
        private int _maxActive;

        public int Attempt { get; } = attempt;

        public int MaxObservedParallelism => Volatile.Read(ref _maxActive);

        public void Enter()
        {
            var active = Interlocked.Increment(ref _active);

            int observed;

            do
            {
                observed = Volatile.Read(ref _maxActive);

                if (active <= observed)
                    break;
            }
            while (Interlocked.CompareExchange(ref _maxActive, active, observed) != observed);
        }

        public void Exit() => Interlocked.Decrement(ref _active);
    }
}
