using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        // 1. Search users
        users.MapGet("/search", async (
            [FromQuery] string? q,
            [FromQuery] int? limit,
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await userService.SearchUsersAsync(
                q ?? string.Empty,
                userId.Value,
                limit ?? 20,
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("SearchUsers")
        .WithSummary("Search for other users by phone number, username, or display name to start chats")
        .Produces<List<UserSearchResultResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. Get user profile
        users.MapGet("/{userId:guid}", async (
            Guid userId,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.GetProfileAsync(userId, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetUserProfile")
        .WithSummary("Get a user's public profile")
        .Produces<UserProfileResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
