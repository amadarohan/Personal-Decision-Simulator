using System.Text;
using DecisionSimulator.Infrastructure.Storage;
using FluentAssertions;

namespace DecisionSimulator.IntegrationTests.Storage;

/// <summary>
/// Exercises the real file system. The path-traversal rules are the reason these
/// tests exist: a fake storage implementation would happily accept a name that
/// the real one must refuse.
/// </summary>
public sealed class LocalDocumentStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalDocumentStorage _storage;

    public LocalDocumentStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"pds-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        _storage = new LocalDocumentStorage(
            new DocumentStorageOptions { RootPath = "documents" },
            _root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_file_is_stored_under_a_generated_name()
    {
        var stored = await _storage.SaveAsync(Content("hello"), "notes.txt", "text/plain");

        stored.FileName.Should().Be("notes.txt");
        stored.RelativePath.Should().NotBe("notes.txt");
        stored.RelativePath.Should().EndWith(".txt");
        stored.SizeBytes.Should().Be(5);
        stored.Checksum.Should().HaveLength(64);

        File.Exists(Path.Combine(_root, "documents", stored.RelativePath)).Should().BeTrue();
    }

    [Fact]
    public async Task Two_uploads_of_the_same_name_do_not_collide()
    {
        var first = await _storage.SaveAsync(Content("one"), "notes.txt", "text/plain");
        var second = await _storage.SaveAsync(Content("two"), "notes.txt", "text/plain");

        first.RelativePath.Should().NotBe(second.RelativePath);

        using var reader = new StreamReader((await _storage.OpenReadAsync(second.RelativePath))!);
        (await reader.ReadToEndAsync()).Should().Be("two");
    }

    [Fact]
    public async Task A_directory_part_in_the_file_name_is_discarded()
    {
        var stored = await _storage.SaveAsync(
            Content("hello"),
            "../../appsettings.json.txt",
            "text/plain");

        // Only the bare name survives, and the file lands inside the root.
        stored.FileName.Should().Be("appsettings.json.txt");
        stored.RelativePath.Should().NotContain("..");
        stored.RelativePath.Should().NotContain("/");
    }

    [Fact]
    public async Task A_disallowed_extension_is_refused()
    {
        var act = async () => await _storage.SaveAsync(Content("MZ"), "payload.exe", "application/octet-stream");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*'.exe'*");
    }

    [Fact]
    public async Task A_file_over_the_size_limit_is_refused_and_not_left_behind()
    {
        var storage = new LocalDocumentStorage(
            new DocumentStorageOptions { RootPath = "documents", MaxFileSizeBytes = 4 },
            _root);

        var act = async () => await storage.SaveAsync(Content("too long"), "notes.txt", "text/plain");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*byte limit*");

        var directory = Path.Combine(_root, "documents");
        Directory.Exists(directory).Should().BeTrue();
        Directory.GetFiles(directory).Should().BeEmpty();
    }

    [Fact]
    public async Task Reading_a_path_outside_the_root_is_refused()
    {
        var act = async () => await _storage.OpenReadAsync("../../appsettings.json");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the document storage root*");
    }

    [Fact]
    public async Task Deleting_a_path_outside_the_root_is_refused()
    {
        var act = async () => await _storage.DeleteAsync("../escape.txt");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the document storage root*");
    }

    [Fact]
    public async Task Reading_a_missing_file_returns_null()
    {
        var stream = await _storage.OpenReadAsync("does-not-exist.txt");

        stream.Should().BeNull();
    }

    [Fact]
    public async Task Deleting_a_missing_file_returns_false()
    {
        var deleted = await _storage.DeleteAsync("does-not-exist.txt");

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_stored_file_can_be_read_back_and_deleted()
    {
        var stored = await _storage.SaveAsync(Content("round trip"), "notes.txt", "text/plain");

        await using (var stream = await _storage.OpenReadAsync(stored.RelativePath))
        {
            stream.Should().NotBeNull();
            using var reader = new StreamReader(stream!, Encoding.UTF8);
            (await reader.ReadToEndAsync()).Should().Be("round trip");
        }

        (await _storage.DeleteAsync(stored.RelativePath)).Should().BeTrue();
        (await _storage.OpenReadAsync(stored.RelativePath)).Should().BeNull();
    }

    [Fact]
    public async Task The_checksum_matches_the_content()
    {
        var stored = await _storage.SaveAsync(Content("hello"), "notes.txt", "text/plain");

        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("hello")));

        stored.Checksum.Should().Be(expected);
    }

    private static MemoryStream Content(string text) =>
        new(Encoding.UTF8.GetBytes(text));
}
