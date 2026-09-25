using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Galleries;

public sealed class AdminGalleryAccessEndpointRefactorTests
{
    [Fact]
    public async Task QueryService_ValidatesGalleryAndReturnsAccesses()
    {
        var gallery = new StubClientGalleryService
        {
            GetGalleryById = id => Task.FromResult<ClientGalleryDetailsDto?>(
                id == 4
                    ? new ClientGalleryDetailsDto { Id = id }
                    : null),
            GetAccesses = id => Task.FromResult(
                new List<GalleryUserAccessDto>
                {
                    new()
                    {
                        UserId = "user-1",
                        Email = "user@example.com",
                        PreviewEnabled = true
                    }
                })
        };
        var service =
            new AdminGalleryAccessQueryService(gallery);

        var invalid = await service.GetAsync(0);
        var missing = await service.GetAsync(3);
        var found = await service.GetAsync(4);

        invalid.StatusCode.Should()
            .Be(StatusCodes.Status400BadRequest);
        missing.StatusCode.Should()
            .Be(StatusCodes.Status404NotFound);
        found.StatusCode.Should()
            .Be(StatusCodes.Status200OK);
        found.Value.Should()
            .BeOfType<List<GalleryUserAccessDto>>()
            .Which.Should().ContainSingle(access =>
                access.UserId == "user-1");
    }

    [Fact]
    public async Task MutationService_UpdatePreservesOldSnapshotAndAuditIdentity()
    {
        var gallery = new StubClientGalleryService
        {
            GetAccesses = _ => Task.FromResult(
                new List<GalleryUserAccessDto>
                {
                    new()
                    {
                        UserId = "user-7",
                        Email = "old@example.com",
                        PreviewEnabled = true
                    }
                }),
            UpdateAccess = (galleryId, userId, _) =>
            {
                galleryId.Should().Be(9);
                userId.Should().Be("user-7");
                return Task.FromResult(true);
            }
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryAccessMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryAccessEndpointService>.Instance);

        var result = await service.UpdateAsync(
            9,
            "user-7",
            new UpdateGalleryAccessRequest
            {
                PreviewEnabled = false,
                DownloadEnabled = true
            },
            Context());

        result.StatusCode.Should()
            .Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "UpdateGalleryAccess" &&
            entry.EntityType == "ClientGalleryAccess" &&
            entry.EntityId == "9:user-7");
    }

    [Fact]
    public async Task MutationService_RemoveReturnsNotFoundWithoutAuditWhenMissing()
    {
        var gallery = new StubClientGalleryService
        {
            GetAccesses = _ => Task.FromResult(
                new List<GalleryUserAccessDto>()),
            RemoveAccess = (_, _) => Task.FromResult(false)
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryAccessMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryAccessEndpointService>.Instance);

        var result = await service.RemoveAsync(
            5,
            "user-2",
            Context());

        result.StatusCode.Should()
            .Be(StatusCodes.Status404NotFound);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task FacadeCompatibilityConstructorPreservesGrantFlow()
    {
        var gallery = new StubClientGalleryService
        {
            GrantAccess = (_, request) =>
                Task.FromResult(
                    request.UserEmail == "user@example.com")
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryAccessEndpointService(
            gallery,
            audit,
            NullLogger<AdminGalleryAccessEndpointService>.Instance);

        var result = await service.GrantAccessAsync(
            6,
            new GrantGalleryAccessRequest
            {
                UserEmail = "user@example.com",
                PreviewEnabled = true,
                DownloadEnabled = true
            },
            Context());

        result.StatusCode.Should()
            .Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "GrantGalleryAccess" &&
            entry.EntityId == "6");
    }

    private static AdminRequestContext Context() =>
        new(
            "admin",
            "admin@example.com",
            "Admin",
            "127.0.0.1",
            "tests",
            "trace");
}
