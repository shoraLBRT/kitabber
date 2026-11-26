using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// Refuses the access token of an ended device session (ADR-0078). The token is a JWT the API checks by the provider's
/// keys alone (ADR-0018); this adds one look at the session, answered from memory for
/// <see cref="AccountSessions.EndedCheckInterval"/>, so a signed-out device stops within a minute instead of the 15 the
/// token lives. Only the API that is its own provider does this: a shared account service would own its sessions.
/// </summary>
internal sealed class EndedSessionValidation(AccountSessions sessions) : IOpenIddictValidationHandler<ValidateTokenContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseScopedHandler<EndedSessionValidation>()
            .SetOrder(OpenIddictValidationHandlers.Protection.ValidateExpirationDate.Descriptor.Order + 1_000)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsRejected || context.Principal is not { } principal)
        {
            return;
        }

        // Every token the provider issues carries its session; one without is not the provider's way and is refused.
        if (ProviderSession.SessionOf(principal) is not { } sessionId
            || await sessions.IsEndedAsync(sessionId, context.CancellationToken))
        {
            context.Reject(
                error: Errors.InvalidToken,
                description: "The session of the token has been ended.");
        }
    }
}
