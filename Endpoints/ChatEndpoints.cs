using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var chats = app.MapGroup("/api/chats")
            .WithTags("Chats")
            .RequireAuthorization();

        chats.MapGet("/", async (
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.GetUserChatsAsync(userId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetUserChats")
        .WithSummary("List all chats for the authenticated user")
        .Produces<List<ChatSummaryResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPost("/direct", async (
            ClaimsPrincipal principal,
            CreateDirectChatRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.CreateDirectChatAsync(userId.Value, request, cancellationToken);
            return result.ToHttpResult(chat => TypedResults.Created($"/api/chats/{chat.Id}", chat));
        })
        .WithName("CreateDirectChat")
        .WithSummary("Create or retrieve a direct 1-on-1 private chat with another user")
        .ProducesValidationProblem()
        .Produces<ChatDetailsResponse>(StatusCodes.Status201Created)
        .Produces<ChatDetailsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPost("/group", async (
            ClaimsPrincipal principal,
            CreateGroupChatRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.CreateGroupChatAsync(userId.Value, request, cancellationToken);
            return result.ToHttpResult(chat => TypedResults.Created($"/api/chats/{chat.Id}", chat));
        })
        .WithName("CreateGroupChat")
        .WithSummary("Create a new group chat with optional initial members")
        .ProducesValidationProblem()
        .Produces<ChatDetailsResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapGet("/{chatId:guid}", async (
            Guid chatId,
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.GetChatDetailsAsync(chatId, userId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetChatById")
        .WithSummary("Get chat metadata and member list by chat ID")
        .Produces<ChatDetailsResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPatch("/{chatId:guid}", async (
            Guid chatId,
            ClaimsPrincipal principal,
            UpdateChatRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.UpdateChatAsync(chatId, userId.Value, request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("UpdateChat")
        .WithSummary("Update group chat title or avatar URL (owner/admin only)")
        .ProducesValidationProblem()
        .Produces<ChatDetailsResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapDelete("/{chatId:guid}", async (
            Guid chatId,
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.DeleteChatAsync(chatId, userId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("DeleteChat")
        .WithSummary("Delete a group chat (owner only) or leave chat")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapGet("/{chatId:guid}/members", async (
            Guid chatId,
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.GetChatMembersAsync(chatId, userId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetChatMembers")
        .WithSummary("List members of a chat")
        .Produces<List<ChatMemberResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPost("/{chatId:guid}/members", async (
            Guid chatId,
            ClaimsPrincipal principal,
            AddChatMemberRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.AddMemberAsync(chatId, userId.Value, request, cancellationToken);
            return result.ToHttpResult(member => TypedResults.Created($"/api/chats/{chatId}/members/{member.UserId}", member));
        })
        .WithName("AddChatMember")
        .WithSummary("Add a user to a group chat (owner/admin only)")
        .ProducesValidationProblem()
        .Produces<ChatMemberResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapDelete("/{chatId:guid}/members/{targetUserId:guid}", async (
            Guid chatId,
            Guid targetUserId,
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.RemoveMemberAsync(chatId, userId.Value, targetUserId, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("RemoveChatMember")
        .WithSummary("Remove a member from a group chat")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPatch("/{chatId:guid}/members/{targetUserId:guid}/role", async (
            Guid chatId,
            Guid targetUserId,
            ClaimsPrincipal principal,
            UpdateMemberRoleRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.UpdateMemberRoleAsync(chatId, userId.Value, targetUserId, request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("UpdateChatMemberRole")
        .WithSummary("Update a member's role (owner only)")
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        chats.MapPost("/{chatId:guid}/leave", async (
            Guid chatId,
            ClaimsPrincipal principal,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await chatService.LeaveChatAsync(chatId, userId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("LeaveChat")
        .WithSummary("Leave a group or private chat")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
