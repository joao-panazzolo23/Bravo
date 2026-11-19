namespace Bravo.Application.Importing;
//todo: trash way to handle things
/// <summary>
/// The TSE years the importer enumerates. Every year for which TSE publishes a candidate
/// archive is included; the list is data, not a user-supplied filter.
/// </summary>
public static class TseYears
{
    /// <summary>
    /// Candidate archives verified to exist from 1994 through 2026 on a two-year cycle.
    /// </summary>
    public static readonly int[] Candidates =
        [1994, 1996, 1998, 2000, 2002, 2004, 2006, 2008, 2010, 2012, 2014, 2016, 2018, 2020, 2022, 2024, 2026];

    /// <summary>
    /// Section-result archives verified to exist from 1998 onwards; 1994 and 1996 return 404.
    /// </summary>
    public static readonly int[] Results =
        [1998, 2000, 2002, 2004, 2006, 2008, 2010, 2012, 2014, 2016, 2018, 2020, 2022, 2024, 2026];
}
