using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Models;
using Bravo.Domain.Geography.Entities;

namespace Bravo.IntegrationTests.Importing;

public sealed class TestStateValidator : IImportRecordValidator<State>
{
    private readonly HashSet<int> _rejectedCodes = [];

    public void Reset() => _rejectedCodes.Clear();

    public void Reject(int ibgeCode) => _rejectedCodes.Add(ibgeCode);

    public ImportRecordValidationResult Validate(State record)
        => _rejectedCodes.Contains(record.IbgeCode)
            ? ImportRecordValidationResult.Invalid($"State '{record.IbgeCode}' was rejected by the test validator.")
            : ImportRecordValidationResult.Valid();
}
