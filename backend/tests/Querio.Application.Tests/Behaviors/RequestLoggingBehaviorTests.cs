using Mediator;
using Microsoft.Extensions.Logging;
using Querio.Application.Common.Behaviors;
using Querio.Application.Tests.Common;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tests.Behaviors;

public sealed class RequestLoggingBehaviorTests
{
    private static readonly IndexDocumentCommand Command = new("report.pdf", 2048);

    [Fact]
    public async Task Logs_a_single_information_event_on_success()
    {
        var logger = new RecordingLogger<RequestLoggingBehavior<IndexDocumentCommand, string>>();
        var behavior = new RequestLoggingBehavior<IndexDocumentCommand, string>(logger);

        var result = await behavior.Handle(
            Command,
            static (_, _) => ValueTask.FromResult("indexed"),
            TestContext.Current.CancellationToken);

        result.ShouldBe("indexed");

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldContain(nameof(IndexDocumentCommand));
        entry.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Logs_deliberate_failures_as_warnings_with_their_error_code()
    {
        var logger = new RecordingLogger<RequestLoggingBehavior<IndexDocumentCommand, string>>();
        var behavior = new RequestLoggingBehavior<IndexDocumentCommand, string>(logger);

        await Should.ThrowAsync<NotFoundException>(async () => await behavior.Handle(
            Command,
            static (_, _) => throw new NotFoundException("Document", "doc_42"),
            TestContext.Current.CancellationToken));

        // An expected 404 must not inflate the error rate.
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("resource.not_found");
    }

    [Fact]
    public async Task Logs_unrecognised_failures_as_errors_with_the_exception()
    {
        var logger = new RecordingLogger<RequestLoggingBehavior<IndexDocumentCommand, string>>();
        var behavior = new RequestLoggingBehavior<IndexDocumentCommand, string>(logger);

        await Should.ThrowAsync<InvalidOperationException>(async () => await behavior.Handle(
            Command,
            static (_, _) => throw new InvalidOperationException("pgvector extension missing"),
            TestContext.Current.CancellationToken));

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Does_not_log_client_cancellation_as_a_fault()
    {
        var logger = new RecordingLogger<RequestLoggingBehavior<IndexDocumentCommand, string>>();
        var behavior = new RequestLoggingBehavior<IndexDocumentCommand, string>(logger);

        await Should.ThrowAsync<OperationCanceledException>(async () => await behavior.Handle(
            Command,
            static (_, _) => throw new OperationCanceledException(),
            TestContext.Current.CancellationToken));

        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rethrows_so_the_pipeline_keeps_its_failure_semantics()
    {
        var logger = new RecordingLogger<RequestLoggingBehavior<IndexDocumentCommand, string>>();
        var behavior = new RequestLoggingBehavior<IndexDocumentCommand, string>(logger);

        var thrown = new ConflictException("Document already ingested.");

        var caught = await Should.ThrowAsync<ConflictException>(async () => await behavior.Handle(
            Command,
            (_, _) => throw thrown,
            TestContext.Current.CancellationToken));

        caught.ShouldBeSameAs(thrown);
    }
}
