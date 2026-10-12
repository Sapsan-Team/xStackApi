using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Endpoints;

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var messages = app.MapGroup("/api/chats/{chatId:guid}")
            .WithTags("Messages")
            .RequireAuthorization();

        // 1. Get messages in chat
        messages.MapGet("/messages", async (
            Guid chatId,
            [FromQuery] int? limit,
            [FromQuery] Guid? before,
            ClaimsPrincipal principal,
            IMessageService messageService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await messageService.GetMessagesAsync(
                chatId,
                userId.Value,
                limit ?? 50,
                before,
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetMessages")
        .WithSummary("Get messages in a chat with cursor pagination")
        .Produces<List<MessageResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. Send message
        messages.MapPost("/messages", async (
            Guid chatId,
            SendMessageRequest request,
            ClaimsPrincipal principal,
            IMessageService messageService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await messageService.SendMessageAsync(
                chatId,
                userId.Value,
                request,
                cancellationToken);

            return result.ToHttpResult(msg => TypedResults.Created($"/api/chats/{chatId}/messages/{msg.Id}", msg));
        })
        .WithName("SendMessage")
        .WithSummary("Send a new message to a chat")
        .ProducesValidationProblem()
        .Produces<MessageResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status401Unauthorized);

        // 3. Edit message
        messages.MapPut("/messages/{messageId:guid}", async (
            Guid chatId,
            Guid messageId,
            EditMessageRequest request,
            ClaimsPrincipal principal,
            IMessageService messageService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await messageService.EditMessageAsync(
                chatId,
                messageId,
                userId.Value,
                request,
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("EditMessage")
        .WithSummary("Edit a message previously sent by the current user")
        .ProducesValidationProblem()
        .Produces<MessageResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // 4. Delete message
        messages.MapDelete("/messages/{messageId:guid}", async (
            Guid chatId,
            Guid messageId,
            ClaimsPrincipal principal,
            IMessageService messageService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await messageService.DeleteMessageAsync(
                chatId,
                messageId,
                userId.Value,
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("DeleteMessage")
        .WithSummary("Delete a message (soft delete)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // 5. Mark messages as read
        messages.MapPost("/read", async (
            Guid chatId,
            MarkMessagesReadRequest? request,
            ClaimsPrincipal principal,
            IMessageService messageService,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return TypedResults.Unauthorized();
            }

            var result = await messageService.MarkAsReadAsync(
                chatId,
                userId.Value,
                request ?? new MarkMessagesReadRequest(),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("MarkMessagesAsRead")
        .WithSummary("Mark messages in a chat as read up to a given message ID (or all if not specified)")
        .Produces<MarkReadResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
