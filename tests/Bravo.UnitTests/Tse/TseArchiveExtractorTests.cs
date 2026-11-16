using System.IO.Compression;
using System.Text;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Tse;

public sealed class TseArchiveExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tse-extract-{Guid.NewGuid():N}");

    public TseArchiveExtractorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private TseArchiveExtractor CreateExtractor(Action<TseOptions>? configure = null)
    {
        var options = new TseOptions { TempDirectory = _root };
        configure?.Invoke(options);

        return new TseArchiveExtractor(
            Options.Create(options),
            NullLogger<TseArchiveExtractor>.Instance);
    }

    private string CreateArchive(string name, params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(_root, name);

        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.Latin1);
            writer.Write(content);
        }

        return path;
    }

    [Fact]
    public void ExtractsValidArchive()
    {
        var archive = CreateArchive("ok.zip", ("data.csv", "A;B\n1;2\n"));
        var destination = Path.Combine(_root, "out");

        var files = CreateExtractor().Extract(archive, destination);

        files.Should().ContainSingle();
        files[0].EntryName.Should().Be("data.csv");
        File.ReadAllText(files[0].FilePath).Should().Be("A;B\n1;2\n");
    }

    [Fact]
    public void RejectsPathTraversalEntry()
    {
        var archive = CreateArchive("evil.zip", ("../../escape.csv", "x"));
        var destination = Path.Combine(_root, "out");

        var act = () => CreateExtractor().Extract(archive, destination);

        act.Should().Throw<TseArchiveException>().WithMessage("*escaping*");
    }

    [Fact]
    public void RejectsAbsolutePathEntry()
    {
        var archive = CreateArchive("abs.zip", ("/etc/passwd", "x"));
        var destination = Path.Combine(_root, "out");

        var act = () => CreateExtractor().Extract(archive, destination);

        act.Should().Throw<TseArchiveException>();
    }

    [Fact]
    public void RejectsTooManyEntries()
    {
        var entries = Enumerable.Range(0, 5)
            .Select(index => ($"file{index}.csv", "x"))
            .ToArray();

        var archive = CreateArchive("many.zip", entries);
        var destination = Path.Combine(_root, "out");

        var act = () => CreateExtractor(options => options.MaxExtractedEntries = 2)
            .Extract(archive, destination);

        act.Should().Throw<TseArchiveException>().WithMessage("*entries*");
    }

    [Fact]
    public void RejectsOversizedEntry()
    {
        var archive = CreateArchive("big.zip", ("big.csv", new string('x', 5000)));
        var destination = Path.Combine(_root, "out");

        var act = () => CreateExtractor(options => options.MaxExtractedBytes = 1000)
            .Extract(archive, destination);

        act.Should().Throw<TseArchiveException>();
    }

    [Fact]
    public void RejectsCorruptArchive()
    {
        var path = Path.Combine(_root, "corrupt.zip");
        File.WriteAllText(path, "this is not a zip");

        var act = () => CreateExtractor().Extract(path, Path.Combine(_root, "out"));

        act.Should().Throw<TseArchiveException>().WithMessage("*not a valid ZIP*");
    }

    [Fact]
    public void RejectsMissingArchive()
    {
        var act = () => CreateExtractor().Extract(
            Path.Combine(_root, "nope.zip"),
            Path.Combine(_root, "out"));

        act.Should().Throw<TseArchiveException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void AppliesEntryFilter()
    {
        var archive = CreateArchive(
            "filter.zip",
            ("keep.csv", "a"),
            ("skip.pdf", "b"));

        var files = CreateExtractor().Extract(
            archive,
            Path.Combine(_root, "out"),
            entryFilter: name => name.EndsWith(".csv"));

        files.Should().ContainSingle();
        files[0].EntryName.Should().Be("keep.csv");
    }
}
