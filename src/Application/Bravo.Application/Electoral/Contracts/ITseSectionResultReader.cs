using Bravo.Application.Electoral.Models;

namespace Bravo.Application.Electoral.Contracts;

public interface ITseSectionResultReader
{
    IReadOnlyList<TseSectionResultRow> Read(Stream stream, string layoutVersion);
}
