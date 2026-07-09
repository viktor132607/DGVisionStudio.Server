using DGVisionStudio.Api.Configuration;
using DGVisionStudio.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace DGVisionStudio.Tests.Api;

public sealed class ApiBehaviorOptionsConfigurationTests
{
    [Fact]
    public void InvalidModelStateResponseFactory_ReturnsStandardApiErrorResponse()
    {
        var options = new ApiBehaviorOptions();
        ApiBehaviorOptionsConfiguration.Configure(options);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-validation"
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        actionContext.ModelState.AddModelError("Title", "Title is required.");
        actionContext.ModelState.AddModelError("Price", "Price is invalid.");

        var result = options.InvalidModelStateResponseFactory(actionContext);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.Code.Should().Be(ApiErrorCodes.ValidationError);
        response.Message.Should().Be("Request validation failed.");
        response.TraceId.Should().Be("trace-validation");
        response.Details.Should().Contain("Title");
        response.Details.Should().Contain("Price");
    }
}
