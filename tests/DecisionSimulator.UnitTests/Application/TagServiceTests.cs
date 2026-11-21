using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Tags;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class TagServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();
    private readonly TagService _service;

    public TagServiceTests()
    {
        _service = new TagService(_database);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_tag_can_be_created_and_listed()
    {
        var created = await _service.CreateAsync(new CreateTagRequest("Career", "#1a2b3c"));

        created.Succeeded.Should().BeTrue();

        var all = await _service.ListAsync();

        all.Value.Should().ContainSingle();
        all.Value[0].Name.Should().Be("Career");
        all.Value[0].ColorHex.Should().Be("#1a2b3c");
        all.Value[0].UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task A_blank_tag_name_is_rejected()
    {
        var result = await _service.CreateAsync(new CreateTagRequest("   "));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task A_duplicate_tag_name_is_rejected()
    {
        await _service.CreateAsync(new CreateTagRequest("Career"));

        var result = await _service.CreateAsync(new CreateTagRequest("Career"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task A_tag_can_be_renamed()
    {
        var created = await _service.CreateAsync(new CreateTagRequest("Old"));

        var updated = await _service.UpdateAsync(
            created.Value,
            new UpdateTagRequest("New", "#ffffff"));

        updated.Succeeded.Should().BeTrue();

        var all = await _service.ListAsync();
        all.Value[0].Name.Should().Be("New");
        all.Value[0].ColorHex.Should().Be("#ffffff");
    }

    [Fact]
    public async Task Renaming_an_unknown_tag_reports_not_found()
    {
        var result = await _service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateTagRequest("New", null));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task A_tag_can_be_attached_to_a_decision()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;
        var decisionId = Guid.NewGuid();

        var attached = await _service.AttachAsync(
            tagId,
            new AttachTagRequest("Decision", decisionId));

        attached.Succeeded.Should().BeTrue();

        var tags = await _service.ListForEntityAsync("Decision", decisionId);
        tags.Value.Should().ContainSingle(t => t.Id == tagId);

        var all = await _service.ListAsync();
        all.Value[0].UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_entity_type_is_rejected()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;

        var result = await _service.AttachAsync(
            tagId,
            new AttachTagRequest("Spaceship", Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ErrorMessage.Should().Contain("not a taggable entity type");
    }

    [Fact]
    public async Task Attaching_an_unknown_tag_reports_not_found()
    {
        var result = await _service.AttachAsync(
            Guid.NewGuid(),
            new AttachTagRequest("Decision", Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task The_same_tag_cannot_be_attached_twice()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;
        var decisionId = Guid.NewGuid();
        var request = new AttachTagRequest("Decision", decisionId);

        await _service.AttachAsync(tagId, request);
        var second = await _service.AttachAsync(tagId, request);

        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task A_tag_can_be_detached()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;
        var decisionId = Guid.NewGuid();
        await _service.AttachAsync(tagId, new AttachTagRequest("Decision", decisionId));

        var detached = await _service.DetachAsync(tagId, "Decision", decisionId);

        detached.Succeeded.Should().BeTrue();

        var tags = await _service.ListForEntityAsync("Decision", decisionId);
        tags.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Detaching_a_link_that_does_not_exist_reports_not_found()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;

        var result = await _service.DetachAsync(tagId, "Decision", Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deleting_a_tag_removes_its_links()
    {
        var tagId = (await _service.CreateAsync(new CreateTagRequest("Career"))).Value;
        var decisionId = Guid.NewGuid();
        await _service.AttachAsync(tagId, new AttachTagRequest("Decision", decisionId));

        var deleted = await _service.DeleteAsync(tagId);

        deleted.Succeeded.Should().BeTrue();

        var all = await _service.ListAsync();
        all.Value.Should().BeEmpty();

        var tags = await _service.ListForEntityAsync("Decision", decisionId);
        tags.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_an_unknown_tag_reports_not_found()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }
}
