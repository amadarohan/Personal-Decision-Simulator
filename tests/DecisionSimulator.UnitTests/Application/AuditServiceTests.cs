using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Audit;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class AuditServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _service = new AuditService(_database);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task An_audit_entry_can_be_recorded_and_read_back()
    {
        var recorded = await _service.RecordAsync(
            "proposal.approved",
            "AIProposal",
            Guid.NewGuid().ToString(),
            "{\"reason\":\"user approved\"}");

        recorded.Succeeded.Should().BeTrue();

        var logs = await _service.QueryAsync(new AuditQuery());

        logs.Value.Should().ContainSingle();
        logs.Value[0].Action.Should().Be("proposal.approved");
        logs.Value[0].EntityType.Should().Be("AIProposal");
        logs.Value[0].Actor.Should().Be("local-user");
    }

    [Fact]
    public async Task An_audit_entry_requires_an_action()
    {
        var result = await _service.RecordAsync("  ", "Decision", null);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task An_audit_entry_requires_an_entity_type()
    {
        var result = await _service.RecordAsync("decision.created", "  ", null);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Entries_can_be_filtered_by_entity_type()
    {
        await _service.RecordAsync("decision.created", "Decision", null);
        await _service.RecordAsync("scenario.created", "Scenario", null);

        var logs = await _service.QueryAsync(new AuditQuery(EntityType: "Decision"));

        logs.Value.Should().ContainSingle();
        logs.Value[0].EntityType.Should().Be("Decision");
    }

    [Fact]
    public async Task Entries_can_be_filtered_by_action()
    {
        await _service.RecordAsync("decision.created", "Decision", null);
        await _service.RecordAsync("decision.updated", "Decision", null);

        var logs = await _service.QueryAsync(new AuditQuery(Action: "decision.updated"));

        logs.Value.Should().ContainSingle();
        logs.Value[0].Action.Should().Be("decision.updated");
    }

    [Fact]
    public async Task Entries_can_be_filtered_by_entity_id()
    {
        var target = Guid.NewGuid().ToString();
        await _service.RecordAsync("decision.created", "Decision", target);
        await _service.RecordAsync("decision.created", "Decision", Guid.NewGuid().ToString());

        var logs = await _service.QueryAsync(new AuditQuery(EntityId: target));

        logs.Value.Should().ContainSingle();
        logs.Value[0].EntityId.Should().Be(target);
    }

    [Fact]
    public async Task Entries_can_be_filtered_by_date_range()
    {
        await _service.RecordAsync("decision.created", "Decision", null);

        var future = await _service.QueryAsync(
            new AuditQuery(FromUtc: DateTime.UtcNow.AddDays(1)));

        future.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task The_page_size_is_capped()
    {
        for (var i = 0; i < 5; i++)
        {
            await _service.RecordAsync($"action.{i}", "Decision", null);
        }

        var logs = await _service.QueryAsync(new AuditQuery(Take: 2));

        logs.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_backup_can_be_recorded_and_listed()
    {
        var recorded = await _service.RecordBackupAsync(new RecordBackupRequest(
            "backup-2026-03-01.zip",
            "backups/backup-2026-03-01.zip",
            2048,
            "abc123",
            IsRestore: false,
            Succeeded: true));

        recorded.Succeeded.Should().BeTrue();

        var backups = await _service.ListBackupsAsync();

        backups.Value.Should().ContainSingle();
        backups.Value[0].FileName.Should().Be("backup-2026-03-01.zip");
        backups.Value[0].SizeBytes.Should().Be(2048);
        backups.Value[0].IsRestore.Should().BeFalse();
    }

    [Fact]
    public async Task Backups_and_restores_can_be_told_apart()
    {
        await _service.RecordBackupAsync(new RecordBackupRequest(
            "backup.zip", "backups/backup.zip", 10, "a", false, true));

        await _service.RecordBackupAsync(new RecordBackupRequest(
            "restore.zip", "backups/restore.zip", 10, "b", true, true));

        var restores = await _service.ListBackupsAsync(isRestore: true);

        restores.Value.Should().ContainSingle();
        restores.Value[0].FileName.Should().Be("restore.zip");
    }

    [Fact]
    public async Task A_backup_without_a_file_name_is_rejected()
    {
        var result = await _service.RecordBackupAsync(new RecordBackupRequest(
            "  ", "backups/x.zip", 10, "a", false, true));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task A_negative_backup_size_is_rejected()
    {
        var result = await _service.RecordBackupAsync(new RecordBackupRequest(
            "backup.zip", "backups/backup.zip", -1, "a", false, true));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }
}
