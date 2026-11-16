using Bravo.Application.Electoral.Models;

namespace Bravo.Application.Electoral.Contracts;

public interface ITseReferenceReader
{
    TseReferenceSnapshot Read(Stream stream, string layoutVersion);
}
