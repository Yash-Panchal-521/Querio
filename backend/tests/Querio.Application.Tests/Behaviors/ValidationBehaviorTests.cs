using FluentValidation;
using Mediator;
using Querio.Application.Common.Behaviors;
using Querio.Application.Tests.Common;
using ValidationException = Querio.Domain.Common.Errors.ValidationException;

namespace Querio.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private const string HandlerResult = "indexed";

    [Fact]
    public async Task Passes_through_when_no_validator_is_registered()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([]);
        var handlerCalls = 0;

        var result = await behavior.Handle(
            new IndexDocumentCommand("report.pdf", 2048),
            Handler(() => handlerCalls++),
            TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerResult);
        handlerCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Invokes_the_handler_when_every_validator_passes()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
            new IndexDocumentCommandFileTypeValidator(),
        ]);
        var handlerCalls = 0;

        var result = await behavior.Handle(
            new IndexDocumentCommand("report.pdf", 2048),
            Handler(() => handlerCalls++),
            TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerResult);
        handlerCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Throws_the_domain_validation_exception_rather_than_FluentValidation_s()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
        ]);

        // The API's exception handler only understands the domain exception; leaking
        // FluentValidation's would surface as a 500.
        await Should.ThrowAsync<ValidationException>(async () => await behavior.Handle(
            new IndexDocumentCommand(string.Empty, 0),
            Handler(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Does_not_invoke_the_handler_when_validation_fails()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
        ]);
        var handlerCalls = 0;

        await Should.ThrowAsync<ValidationException>(async () => await behavior.Handle(
            new IndexDocumentCommand(string.Empty, 0),
            Handler(() => handlerCalls++),
            TestContext.Current.CancellationToken));

        handlerCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Groups_failures_by_field()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
        ]);

        var exception = await Should.ThrowAsync<ValidationException>(async () => await behavior.Handle(
            new IndexDocumentCommand(string.Empty, 0),
            Handler(),
            TestContext.Current.CancellationToken));

        exception.Errors.Keys.ShouldBe(["FileName", "SizeInBytes"], ignoreOrder: true);
        exception.Errors["FileName"].ShouldContain("File name is required.");
        exception.Errors["SizeInBytes"].ShouldContain("File must not be empty.");
    }

    [Fact]
    public async Task Merges_failures_from_every_validator_for_the_same_field()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
            new IndexDocumentCommandFileTypeValidator(),
        ]);

        var exception = await Should.ThrowAsync<ValidationException>(async () => await behavior.Handle(
            new IndexDocumentCommand("notes.txt", 512),
            Handler(),
            TestContext.Current.CancellationToken));

        // Only the file-type rule fails here, but it comes from the second validator.
        exception.Errors["FileName"].ShouldContain("Only PDF uploads are supported.");
    }

    [Fact]
    public async Task Reports_the_validation_error_code_the_client_branches_on()
    {
        var behavior = new ValidationBehavior<IndexDocumentCommand, string>([
            new IndexDocumentCommandValidator(),
        ]);

        var exception = await Should.ThrowAsync<ValidationException>(async () => await behavior.Handle(
            new IndexDocumentCommand(string.Empty, 0),
            Handler(),
            TestContext.Current.CancellationToken));

        exception.ErrorCode.ShouldBe("request.validation_failed");
        exception.Extensions.ShouldContainKey("errors");
    }

    private static MessageHandlerDelegate<IndexDocumentCommand, string> Handler(Action? onCalled = null) =>
        (_, _) =>
        {
            onCalled?.Invoke();

            return ValueTask.FromResult(HandlerResult);
        };
}
