using DGVisionStudio.Api.Configuration;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Tests.Architecture;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void AddDGVisionApplicationServices_UsesFactories_ForFacadeServicesWithCompatibilityConstructors()
    {
        var services = new ServiceCollection();

        services.AddDGVisionApplicationServices(new StorageOptions { Provider = "FileSystem" });

        var facadeTypes = new[]
        {
            typeof(IHomeSlideshowService),
            typeof(IAuthService),
            typeof(IAdminPortfolioService),
            typeof(IAdminGalleryMediaManagementService),
            typeof(IAdminPrintRequestService),
            typeof(IAdminClientGalleryManagementService),
            typeof(IClientGalleryAdminService),
            typeof(IClientGalleryUserService),
            typeof(IClientGalleryPhotoService)
        };

        foreach (var serviceType in facadeTypes)
        {
            var descriptor = services.LastOrDefault(x => x.ServiceType == serviceType);

            descriptor.Should().NotBeNull($"{serviceType.Name} must be registered");
            descriptor!.ImplementationFactory.Should().NotBeNull(
                $"{serviceType.Name} keeps compatibility constructors and must use an explicit DI factory");
            descriptor.ImplementationType.Should().BeNull();
        }
    }

    [Fact]
    public void AddDGVisionApplicationServices_HasNoUnsafeAutomaticMultiConstructorRegistrations()
    {
        var services = new ServiceCollection();

        services.AddDGVisionApplicationServices(new StorageOptions { Provider = "FileSystem" });

        var unsafeRegistrations = services
            .Where(x => x.ImplementationType is not null)
            .Select(x => x.ImplementationType!)
            .Distinct()
            .Where(HasAmbiguousConstructorShape)
            .Select(x => x.FullName)
            .ToArray();

        unsafeRegistrations.Should().BeEmpty(
            "automatic DI activation must not be used for types with unrelated public constructors");
    }

    private static bool HasAmbiguousConstructorShape(Type implementationType)
    {
        var constructorParameterSets = implementationType
            .GetConstructors()
            .Select(constructor => constructor
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToHashSet())
            .ToArray();

        if (constructorParameterSets.Length < 2)
            return false;

        var maximalConstructors = constructorParameterSets
            .Where(candidate => !constructorParameterSets.Any(other =>
                !ReferenceEquals(candidate, other) &&
                candidate.IsProperSubsetOf(other)))
            .ToArray();

        return maximalConstructors.Length > 1;
    }
}
