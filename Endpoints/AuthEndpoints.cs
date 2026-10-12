using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/register", async Task<Results<Created<AuthTokenResponse>, Conflict<ProblemDetails>>> (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.RegisterAsync(request, cancellationToken);
            return response is null
                ? TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Account already exists",
                    Detail = "The phone number or username is already registered."
                })
                : TypedResults.Created("/api/auth/me", response);
        })
        .WithName("Register")
        .WithSummary("Register a user and issue an access token")
        .ProducesValidationProblem()
        .Produces<AuthTokenResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            return response is null
                ? TypedResults.Unauthorized()
                : TypedResults.Ok(response);
        })
        .WithName("Login")
        .WithSummary("Authenticate with a phone number and password")
        .ProducesValidationProblem()
        .Produces<AuthTokenResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", async Task<Results<Ok<AuthUserResponse>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
            {
                var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (!Guid.TryParse(id, out var userId))
                {
                    return TypedResults.Unauthorized();
                }

                var user = await authService.GetUserAsync(userId, cancellationToken);
                return user is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(user);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user's profile from the database")
            .Produces<AuthUserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
