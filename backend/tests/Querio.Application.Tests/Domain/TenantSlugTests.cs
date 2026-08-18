using Querio.Domain.Tenants;

namespace Querio.Application.Tests.Domain;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("Ada Corp", "ada-corp")]
    [InlineData("  Trailing And Leading  ", "trailing-and-leading")]
    [InlineData("Acme, Inc.", "acme-inc")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("Hyphen-Already", "hyphen-already")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("Numbers 123", "numbers-123")]
    public void Names_become_readable_slugs(string name, string expected) =>
        TenantSlug.From(name).ShouldBe(expected);

    [Fact]
    public void Accents_are_folded_rather_than_dropped()
    {
        // Dropping them would turn "Café" into "caf", which reads as a typo.
        TenantSlug.From("Café Ltd").ShouldBe("cafe-ltd");
    }

    [Fact]
    public void A_name_of_only_symbols_still_produces_a_usable_slug()
    {
        // Otherwise the slug is empty and the organization gets an unroutable URL.
        TenantSlug.From("!!!").ShouldBe("org");
    }

    [Fact]
    public void Long_names_are_truncated_without_a_trailing_hyphen()
    {
        var slug = TenantSlug.From(new string('a', 40) + " " + new string('b', 40));

        slug.Length.ShouldBeLessThanOrEqualTo(48);
        slug.ShouldNotEndWith("-");
    }

    [Fact]
    public void Suffixes_keep_the_slug_within_its_limit()
    {
        var slug = TenantSlug.WithSuffix(new string('a', 48), 12);

        slug.Length.ShouldBeLessThanOrEqualTo(48);
        slug.ShouldEndWith("-12");
    }
}
