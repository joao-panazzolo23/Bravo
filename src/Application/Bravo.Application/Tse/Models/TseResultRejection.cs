namespace Bravo.Application.Tse.Models;

/// <summary>
/// Why a source row could not be turned into a result key.
/// </summary>
/// <remarks>
/// The distinction matters for progress reporting: a row the source itself cannot resolve is a
/// rejection, not a silent success, and it must be counted so the job can report
/// <c>CompletedWithErrors</c> instead of pretending everything was imported.
/// </remarks>
public enum TseResultRejection
{
    /// <summary>The row was resolved.</summary>
    None,

    /// <summary>The TSE municipality code has no IBGE mapping.</summary>
    Municipality,

    /// <summary>The zone or section is not part of the prepared structure.</summary>
    Structure,

    /// <summary>The office code is not imported.</summary>
    Office,

    /// <summary>The election or round code is not imported.</summary>
    Election,

    /// <summary>A nominal vote references a candidate that was not imported.</summary>
    Candidate,

    /// <summary>A party vote references a party that was not imported.</summary>
    Party
}
