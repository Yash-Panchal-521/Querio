using Querio.Domain.Common.Errors;

namespace Querio.Application.Tests.Domain;

/// <summary>
/// What a validation failure says for itself.
///
/// The message becomes ProblemDetails' <c>detail</c>, which is what a client shows. A single
/// error hiding behind a plural summary is not a cosmetic problem: it is the difference between
/// a person being told the file type is unsupported and being told that one or more validation
/// errors occurred.
/// </summary>
public sealed class ValidationExceptionTests
{
    [Fact]
    public void A_single_error_is_its_own_message()
    {
        var failure = new ValidationException("file", "That file type is not supported.");

        failure.Message.ShouldBe("That file type is not supported.");
    }

    [Fact]
    public void Several_errors_get_a_summary()
    {
        var failure = new ValidationException(new Dictionary<string, string[]>
        {
            ["name"] = ["A name is required."],
            ["slug"] = ["That address is taken."],
        });

        // No single sentence covers two fields, and picking one would hide the other. The
        // specific messages stay reachable under `errors`.
        failure.Message.ShouldBe("One or more validation errors occurred.");
        failure.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void Several_errors_on_one_field_get_a_summary_too()
    {
        var failure = new ValidationException(new Dictionary<string, string[]>
        {
            ["file"] = ["The file is empty.", "That file type is not supported."],
        });

        failure.Message.ShouldBe("One or more validation errors occurred.");
    }

    [Fact]
    public void A_blank_error_does_not_become_the_message()
    {
        // Guards the shortcut rather than the caller: a message of whitespace would read as the
        // request having failed for no stated reason at all.
        var failure = new ValidationException(new Dictionary<string, string[]>
        {
            ["file"] = ["   "],
        });

        failure.Message.ShouldBe("One or more validation errors occurred.");
    }
}
