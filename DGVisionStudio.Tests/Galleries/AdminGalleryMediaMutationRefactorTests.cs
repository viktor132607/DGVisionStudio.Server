using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Galleries;

public sealed class AdminGalleryMediaMutationRefactorTests
{
    [Fact]
    public async Task PhotoMutation_UpdatePreservesSnapshotDelegateAndAuditFlow()
    {
        var updateCalls = 0;
        var gallery = new StubClientGalleryService
        {
            GetGalleryById = _ => Task.FromResult<ClientGalleryDetailsDto?>(
                new ClientGalleryDetailsDto
                {
                    Id = 4,
                    Photos =
                    [
                        new ClientPhotoDto
                        {
                            Id = 8,
                            DisplayOrder = 2
                        }
                    ]
                }),
            UpdatePhoto = (galleryId, photoId, _) =>
            {
                galleryId.Should().Be(4);
                photoId.Should().Be(8);
                updateCalls++;
                return Task.FromResult<ClientPhotoDto?>(
                    new ClientPhotoDto
                    {
                        Id = photoId,
                        DisplayOrder = 3
                    });
            }
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryPhotoMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryMediaMutationService>.Instance);

        var result = await service.UpdateAsync(
            4,
            8,
            new UpdateClientPhotoRequest
            {
                IsPublished = true
            },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        updateCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "UpdateGalleryPhoto" &&
            entry.EntityType == "ClientGalleryPhoto" &&
            entry.EntityId == "8");
    }

    [Fact]
    public async Task PhotoMutation_DeleteReturnsNotFoundWithoutAuditWhenDelegateFails()
    {
        var gallery = new StubClientGalleryService
        {
            GetGalleryById = _ => Task.FromResult<ClientGalleryDetailsDto?>(
                new ClientGalleryDetailsDto
                {
                    Id = 2,
                    Photos =
                    [
                        new ClientPhotoDto { Id = 5 }
                    ]
                }),
            DeletePhoto = (_, _) => Task.FromResult(false)
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryPhotoMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryMediaMutationService>.Instance);

        var result = await service.DeleteAsync(
            2,
            5,
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ArrangementMutation_SetCoverPreservesValidationDelegateAndAudit()
    {
        string? capturedCover = null;
        var gallery = new StubClientGalleryService
        {
            GetGalleryById = id => Task.FromResult<ClientGalleryDetailsDto?>(
                new ClientGalleryDetailsDto { Id = id }),
            SetCover = (galleryId, cover) =>
            {
                galleryId.Should().Be(6);
                capturedCover = cover;
                return Task.FromResult(true);
            }
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryArrangementMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryMediaMutationService>.Instance);

        var result = await service.SetCoverAsync(
            6,
            new SetGalleryCoverRequest
            {
                CoverImageUrl = "/uploads/cover.jpg"
            },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        capturedCover.Should().Be("/uploads/cover.jpg");
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "SetGalleryCover" &&
            entry.EntityType == "ClientGallery" &&
            entry.EntityId == "6");
    }

    [Fact]
    public async Task ArrangementMutation_ReorderPreservesPhotoIdsAndAudit()
    {
        List<int>? captured = null;
        var gallery = new StubClientGalleryService
        {
            GetGalleryById = id => Task.FromResult<ClientGalleryDetailsDto?>(
                new ClientGalleryDetailsDto
                {
                    Id = id,
                    Photos =
                    [
                        new ClientPhotoDto
                        {
                            Id = 11,
                            DisplayOrder = 1
                        },
                        new ClientPhotoDto
                        {
                            Id = 12,
                            DisplayOrder = 2
                        }
                    ]
                }),
            Reorder = (galleryId, ids) =>
            {
                galleryId.Should().Be(9);
                captured = ids;
                return Task.FromResult(true);
            }
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryArrangementMutationService(
            gallery,
            new AdminGalleryMutationAuditService(audit),
            NullLogger<AdminGalleryMediaMutationService>.Instance);

        var result = await service.ReorderAsync(
            9,
            new ReorderGalleryPhotosRequest
            {
                OrderedPhotoIds = [12, 11]
            },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        captured.Should().Equal(12, 11);
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "ReorderGalleryPhotos" &&
            entry.EntityId == "9");
    }

    [Fact]
    public async Task FacadeCompatibilityConstructorPreservesExistingCallers()
    {
        var gallery = new StubClientGalleryService
        {
            DeletePhoto = (_, _) => Task.FromResult(true)
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryMediaMutationService(
            gallery,
            audit,
            NullLogger<AdminGalleryMediaMutationService>.Instance);

        var result = await service.DeletePhotoAsync(
            3,
            7,
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(entry =>
            entry.Action == "DeleteGalleryPhoto" &&
            entry.EntityId == "7");
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
