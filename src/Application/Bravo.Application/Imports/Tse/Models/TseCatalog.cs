namespace Bravo.Application.Imports.Tse.Models;

/// <summary>
/// Static facts about the TSE datasets: which years exist, which states publish results, and
/// the parallelism each stage defaults to.
/// </summary>
/// <remarks>
/// This is data, not configuration: TSE publishes on a fixed two-year cycle, and a year only
/// becomes importable once its archive exists. The lists are therefore code, while execution
/// limits stay in <c>ImportEngineOptions</c>.
/// </remarks>
public static class TseCatalog
{
    /// <summary>Candidate archives verified from 1994 onward, on a two-year cycle.</summary>
    public static readonly int[] CandidateYears =
    [
        1994, 1996, 1998, 2000, 2002, 2004, 2006, 2008,
        2010, 2012, 2014, 2016, 2018, 2020, 2022, 2024, 2026
    ];

    /// <summary>Section-result archives verified from 1998 onward; 1994 and 1996 return 404.</summary>
    public static readonly int[] ResultYears =
    [
        1998, 2000, 2002, 2004, 2006, 2008, 2010, 2012,
        2014, 2016, 2018, 2020, 2022, 2024, 2026
    ];

    /// <summary>
    /// The 27 states plus the exterior scope ("ZZ"), which TSE publishes from 2026. A state
    /// absent from a given year's archive returns 404 and is skipped rather than failing the job.
    /// </summary>
    public static readonly string[] ResultStates =
    [
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP",
        "TO", "ZZ"
    ];

    public static int ReferenceOperationCount => CandidateYears.Length;

    public static int CandidateOperationCount => CandidateYears.Length;

    public static int ResultOperationCount => ResultYears.Length * ResultStates.Length;
}
