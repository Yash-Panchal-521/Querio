using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Querio.Api.Common.Authentication;
using Querio.Application.Common.Abstractions;

namespace Querio.Api.Extensions;

internal static class AuthenticationExtensions
{
    public static WebApplicationBuilder AddQuerioAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<FirebaseAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(FirebaseAuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at start-up, not at the first request: a missing project id would
            // otherwise produce an issuer of "https://securetoken.google.com/" that no token
            // matches, and every request would 401 with no clue why.
            .ValidateOnStart();

        var firebase = builder.Configuration
            .GetSection(FirebaseAuthenticationOptions.SectionName)
            .Get<FirebaseAuthenticationOptions>() ?? new FirebaseAuthenticationOptions();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Discovery gives us Google's rotating signing keys without an Admin SDK.
                options.Authority = firebase.Issuer;
                options.Audience = firebase.Audience;

                // Keep JWT claim names as issued. Without this, ASP.NET rewrites "sub" to a
                // SOAP-era URI and every claim lookup becomes a guess.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // These two are the security boundary, not hardening.
                    //
                    // Every Firebase project's tokens are signed with the same shared Google
                    // keys, so a token minted in anyone's free project carries a genuinely
                    // valid signature here. Firebase derives both iss and aud from the project
                    // id, so either check alone rejects a foreign token — which is exactly why
                    // both are validated and each is covered by its own test. Relaxing either
                    // one leaves no failing test behind if the other still stands.
                    ValidateIssuer = true,
                    ValidIssuer = firebase.Issuer,

                    ValidateAudience = true,
                    ValidAudience = firebase.Audience,

                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

                    // Firebase tokens are short-lived; a generous skew would widen the window
                    // in which a revoked session still works.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = FirebaseClaims.Subject,
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Firebase guarantees a non-empty sub, but a token that reaches here
                        // without one would produce a user row keyed on an empty string.
                        var subject = context.Principal?.FindFirst(FirebaseClaims.Subject)?.Value;

                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            context.Fail("Token does not carry a subject claim.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        // Authorization policies are registered separately, in AddQuerioAuthorization.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return builder;
    }
}
