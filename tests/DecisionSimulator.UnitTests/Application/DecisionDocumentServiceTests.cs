using System.Text;
using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Documents;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class DecisionDocumentServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly DecisionDocumentService _service;

    public DecisionDocumentServiceTests()
    {
        _service = new DecisionDocumentService(_database, _storage);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_document_can_be_uploaded_and_listed()
    {
        var decisionId = await CreateDecisionAsync();

        var uploaded = await _service.UploadAsync(
            decisionId,
            Request("notes.txt", "hello"));

        uploaded.Succeeded.Should().BeTrue();
        uploaded.Value.FileName.Should().Be("notes.txt");
        uploaded.Value.FileSizeBytes.Should().Be(5);
        uploaded.Value.Checksum.Should().NotBeNullOrWhiteSpace();

        var documents = await _service.ListAsync(decisionId);

        documents.Value.Should().ContainSingle();
        documents.Value[0].Id.Should().Be(uploaded.Value.Id);
    }

    [Fact]
    public async Task Uploading_to_an_unknown_decision_reports_not_found()
    {
        var result = await _service.UploadAsync(Guid.NewGuid(), Request("notes.txt", "hello"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
        _storage.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task A_document_without_a_file_name_is_rejected()
    {
        var decisionId = await CreateDecisionAsync();

        var result = await _service.UploadAsync(decisionId, Request("  ", "hello"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        _storage.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_file_type_does_not_leave_a_row()
    {
        var decisionId = await CreateDecisionAsync();
        _storage.RejectNextSave = true;

        var result = await _service.UploadAsync(decisionId, Request("payload.exe", "MZ"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);

        var documents = await _service.ListAsync(decisionId);
        documents.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task A_stored_document_can_be_opened()
    {
        var decisionId = await CreateDecisionAsync();
        var uploaded = await _service.UploadAsync(decisionId, Request("notes.txt", "hello"));

        var opened = await _service.OpenAsync(uploaded.Value.Id);

        opened.Succeeded.Should().BeTrue();

        using var reader = new StreamReader(opened.Value, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();

        text.Should().Be("hello");
    }

    [Fact]
    public async Task Opening_an_unknown_document_reports_not_found()
    {
        var result = await _service.OpenAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task A_document_whose_file_vanished_reports_not_found()
    {
        var decisionId = await CreateDecisionAsync();
        var uploaded = await _service.UploadAsync(decisionId, Request("notes.txt", "hello"));

        _storage.RemoveAllFiles();

        var opened = await _service.OpenAsync(uploaded.Value.Id);

        opened.Succeeded.Should().BeFalse();
        opened.ErrorCode.Should().Be(ErrorCodes.NotFound);
        opened.ErrorMessage.Should().Contain("missing from storage");
    }

    [Fact]
    public async Task Deleting_a_document_removes_the_row_and_the_file()
    {
        var decisionId = await CreateDecisionAsync();
        var uploaded = await _service.UploadAsync(decisionId, Request("notes.txt", "hello"));

        var deleted = await _service.DeleteAsync(uploaded.Value.Id);

        deleted.Succeeded.Should().BeTrue();

        var documents = await _service.ListAsync(decisionId);
        documents.Value.Should().BeEmpty();
        _storage.DeleteCount.Should().Be(1);
    }

    [Fact]
    public async Task Deleting_an_unknown_document_reports_not_found()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Listing_documents_for_an_unknown_decision_reports_not_found()
    {
        var result = await _service.ListAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    private async Task<Guid> CreateDecisionAsync()
    {
        var decision = new Decision("Move abroad", DecisionCategory.Relocation);
        _database.Context.Decisions.Add(decision);
        await _database.SaveChangesAsync();
        return decision.Id;
    }

    private static UploadDocumentRequest Request(string fileName, string content) =>
        new(fileName, "text/plain", new MemoryStream(Encoding.UTF8.GetBytes(content)));

    /// <summary>
    /// An in-memory stand-in for local storage. It records how often it was
    /// called so the tests can prove the service does not touch storage when it
    /// has already decided to reject the request.
    /// </summary>
    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

        public bool RejectNextSave { get; set; }

        /// <summary>Simulates a file that disappeared from disk behind the app's back.</summary>
        public void RemoveAllFiles() => _files.Clear();

        public Task<StoredFile> SaveAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;

            if (RejectNextSave)
            {
                throw new NotSupportedException(
                    $"Files with the extension '{Path.GetExtension(fileName)}' cannot be attached.");
            }

            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            var bytes = buffer.ToArray();

            var relativePath = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
            _files[relativePath] = bytes;

            return Task.FromResult(new StoredFile(
                fileName,
                relativePath,
                contentType,
                bytes.Length,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))));
        }

        public Task<Stream?> OpenReadAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (!_files.TryGetValue(relativePath, out var bytes))
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
        }

        public Task<bool> DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            return Task.FromResult(_files.Remove(relativePath));
        }
    }
}
