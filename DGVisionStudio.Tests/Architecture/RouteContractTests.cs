using System.Reflection;
using DGVisionStudio.Infrastructure.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DGVisionStudio.Tests.Architecture;

public sealed class RouteContractTests
{
    [Fact]
    public void ControllerRoutes_HaveNoDuplicateHttpMethodAndTemplatePairs()
    {
        var endpoints = DiscoverEndpoints();

        var duplicates = endpoints
            .GroupBy(x => (x.HttpMethod, x.Route), StringTupleComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.HttpMethod} {group.Key.Route}: {string.Join(", ", group.Select(x => x.Action))}")
            .ToArray();

        duplicates.Should().BeEmpty("each HTTP method and route pair must map to exactly one action");
    }

    [Fact]
    public void ContactMarkAllSeen_UsesTheAdminRouteWithoutThePublicContactPrefix()
    {
        var endpoint = DiscoverEndpoints().Single(x =>
            x.Action == $"{nameof(ContactRequestsController)}.{nameof(ContactRequestsController.MarkAllSeen)}");

        endpoint.HttpMethod.Should().Be("PUT");
        endpoint.Route.Should().Be("/api/admin/contact-requests/seen");
    }

    private static IReadOnlyList<EndpointDescriptor> DiscoverEndpoints()
    {
        return typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(DiscoverControllerEndpoints)
            .ToArray();
    }

    private static IEnumerable<EndpointDescriptor> DiscoverControllerEndpoints(Type controllerType)
    {
        var controllerTemplates = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .DefaultIfEmpty(string.Empty)
            .ToArray();

        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            var httpAttributes = method
                .GetCustomAttributes(inherit: true)
                .OfType<HttpMethodAttribute>()
                .ToArray();

            foreach (var httpAttribute in httpAttributes)
            {
                foreach (var httpMethod in httpAttribute.HttpMethods)
                {
                    foreach (var controllerTemplate in controllerTemplates)
                    {
                        yield return new EndpointDescriptor(
                            httpMethod.ToUpperInvariant(),
                            CombineRoute(controllerTemplate, httpAttribute.Template),
                            $"{controllerType.Name}.{method.Name}");
                    }
                }
            }
        }
    }

    private static string CombineRoute(string? controllerTemplate, string? actionTemplate)
    {
        if (!string.IsNullOrWhiteSpace(actionTemplate) &&
            (actionTemplate.StartsWith('/') || actionTemplate.StartsWith("~/", StringComparison.Ordinal)))
        {
            return NormalizeRoute(actionTemplate.TrimStart('~'));
        }

        var parts = new[] { controllerTemplate, actionTemplate }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim('/'));

        return NormalizeRoute('/' + string.Join('/', parts));
    }

    private static string NormalizeRoute(string route)
    {
        var normalized = '/' + route.Trim().Trim('/');
        return normalized == "/" ? normalized : normalized.ToLowerInvariant();
    }

    private sealed record EndpointDescriptor(string HttpMethod, string Route, string Action);

    private sealed class StringTupleComparer : IEqualityComparer<(string HttpMethod, string Route)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();

        public bool Equals((string HttpMethod, string Route) x, (string HttpMethod, string Route) y) =>
            string.Equals(x.HttpMethod, y.HttpMethod, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Route, y.Route, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string HttpMethod, string Route) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.HttpMethod),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Route));
    }
}
