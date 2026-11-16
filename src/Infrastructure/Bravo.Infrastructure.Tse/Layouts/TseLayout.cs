using System.Text;

namespace Bravo.Infrastructure.Tse.Layouts;

public sealed record TseLayout(
    string Version,
    char Delimiter,
    char Quote,
    Encoding Encoding,
    IReadOnlyDictionary<string, string> Columns,
    IReadOnlySet<string> RequiredCanonicalFields)
{
    public static TseLayout Create(
        string version,
        char delimiter,
        char quote,
        Encoding encoding,
        IReadOnlyDictionary<string, string> columns,
        IReadOnlyCollection<string> requiredCanonicalFields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(requiredCanonicalFields);

        var declared = new HashSet<string>(columns.Values, StringComparer.Ordinal);

        var undeclared = requiredCanonicalFields
            .Where(field => !declared.Contains(field))
            .ToArray();

        if (undeclared.Length > 0)
        {
            throw new ArgumentException(
                $"Layout '{version}' requires canonical fields it does not declare: " +
                string.Join(", ", undeclared) + ".",
                nameof(requiredCanonicalFields));
        }

        return new TseLayout(
            version,
            delimiter,
            quote,
            encoding,
            columns,
            new HashSet<string>(requiredCanonicalFields, StringComparer.Ordinal));
    }
}
