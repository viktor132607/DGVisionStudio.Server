using DGVisionStudio.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioArchiveJobsRefactorTests
{
    [Fact]
    public void Registry_PreservesValidationAndIdempotentRetryRules()
    {
        var registry = CreateRegistry();

        var missingOwner = () =>
            registry.Enqueue(" ", null);
        var emptySelection = () =>
            registry.Enqueue("admin-1", []);
        var invalidSelection = () =>
            registry.Enqueue(
                "admin-1",
                [1, -2]);

        missingOwner.Should()
            .Throw<ArchiveRequestException>()
            .WithMessage(
                "Влез отново в администраторския профил.");
        emptySelection.Should()
            .Throw<ArchiveRequestException>()
            .WithMessage(
                "Маркирай поне един валиден албум.");
        invalidSelection.Should()
            .Throw<ArchiveRequestException>()
            .WithMessage(
                "Маркирай поне един валиден албум.");

        var created =
            registry.Enqueue(
                "admin-1",
                [3, 2, 2]);

        var retried =
            registry.Enqueue(
                "admin-1",
                [2, 3]);

        retried.Id.Should().Be(created.Id);

        var conflicting = () =>
            registry.Enqueue(
                "admin-1",
                [4]);

        conflicting.Should()
            .Throw<ArchiveRequestException>()
            .WithMessage(
                "Вече се подготвя друг архив. Изчакай да завърши.");
    }

    [Fact]
    public void Registry_PreservesOwnerIsolationAndQueuedRemovalPolicy()
    {
        var registry = CreateRegistry();
        var status =
            registry.Enqueue(
                "admin-1",
                null);

        registry.Get(status.Id, "admin-1")
            .Should().NotBeNull();
        registry.Get(status.Id, "admin-2")
            .Should().BeNull();

        registry.RemoveAsync(
                status.Id,
                "admin-1")
            .GetAwaiter()
            .GetResult()
            .Should().BeFalse();
    }

    [Fact]
    public void Registry_PreservesBoundedQueueBackpressureMessage()
    {
        var registry = CreateRegistry();

        for (var index = 0; index < 8; index++)
        {
            registry.Enqueue(
                $"admin-{index}",
                null);
        }

        var overflow = () =>
            registry.Enqueue(
                "admin-overflow",
                null);

        overflow.Should()
            .Throw<ArchiveRequestException>()
            .WithMessage(
                "В момента се подготвят други архиви. Опитай отново след малко.");
    }

    [Fact]
    public void FacadeCompatibilityConstructor_DelegatesPublicJobState()
    {
        var services =
            new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

        var jobs = new PortfolioArchiveJobs(
            services.GetRequiredService<
                IServiceScopeFactory>(),
            NullLogger<
                PortfolioArchiveJobs>.Instance);

        var created =
            jobs.Enqueue(
                "admin-1",
                [7, 7]);

        created.Status.Should().Be("queued");
        jobs.Get(created.Id, "admin-1")
            .Should().BeEquivalentTo(created);
        jobs.Get(created.Id, "other-admin")
            .Should().BeNull();
    }

    private static PortfolioArchiveJobRegistry CreateRegistry()
    {
        var queue =
            new PortfolioArchiveJobQueue();
        var files =
            new PortfolioArchiveJobFileService(
                NullLogger<
                    PortfolioArchiveJobs>.Instance);

        return new(
            queue,
            files);
    }
}
