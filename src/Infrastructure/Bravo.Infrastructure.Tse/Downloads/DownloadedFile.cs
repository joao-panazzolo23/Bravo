namespace Bravo.Infrastructure.Tse.Downloads;

public sealed record DownloadedFile(
    string FilePath,
    string FileName,
    long SizeBytes,
    string Sha256,
    string SourceUrl);
