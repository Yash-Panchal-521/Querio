using System.Text.Json.Serialization;

namespace Querio.Api.Extensions;

internal static class JsonExtensions
{
    public static WebApplicationBuilder AddQuerioJson(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            // Enums as names, not ordinals. A client reading "Owner" keeps working when the
            // underlying numbers are renumbered to make room for a new role; a client that
            // learned 30 breaks silently and starts granting the wrong access.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

            // Absent rather than null, so responses stay small and clients distinguish
            // "no value" from "not sent".
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return builder;
    }
}
