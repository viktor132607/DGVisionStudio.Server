using DGVisionStudio.Infrastructure.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Auth;

public sealed class AuthControllerValidationTests
{
    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var controller = CreateController();
        var request = CreateRequest(nameof(AuthController.Register), "Email", "", "Password", "", "ConfirmPassword", "");

        var result = await Invoke(controller, nameof(AuthController.Register), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordsDoNotMatch()
    {
        var controller = CreateController();
        var request = CreateRequest(
            nameof(AuthController.Register),
            "Email", "user@example.com",
            "Password", "Password123!",
            "ConfirmPassword", "Different123!");

        var result = await Invoke(controller, nameof(AuthController.Register), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var controller = CreateController();
        var request = CreateRequest(nameof(AuthController.Login), "Email", "", "Password", "");

        var result = await Invoke(controller, nameof(AuthController.Login), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_ReturnsBadRequest_WhenEmailIsMissing()
    {
        var controller = CreateController();
        var request = CreateRequest(nameof(AuthController.ForgotPassword), "Email", " ");

        var result = await Invoke(controller, nameof(AuthController.ForgotPassword), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ResetPasswordPage_RedirectsToLogin_WhenQueryIsInvalid()
    {
        var controller = CreateController();

        var result = controller.ResetPasswordPage("", "");

        var redirect = result.Should().BeOfType<RedirectResult>().Which;
        redirect.Url.Should().Be("http://localhost:5173/identity/login");
    }

    [Fact]
    public async Task ResetPassword_ReturnsBadRequest_WhenRequestIsInvalid()
    {
        var controller = CreateController();
        var request = CreateRequest(
            nameof(AuthController.ResetPassword),
            "Email", "",
            "Token", "",
            "Password", "",
            "ConfirmPassword", "");

        var result = await Invoke(controller, nameof(AuthController.ResetPassword), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var controller = CreateController();
        var request = CreateRequest(
            nameof(AuthController.ChangePassword),
            "CurrentPassword", "",
            "NewPassword", "",
            "ConfirmPassword", "");

        var result = await Invoke(controller, nameof(AuthController.ChangePassword), request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AuthController CreateController()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Url"] = "http://localhost:5173"
            })
            .Build();

        return new AuthController(
            null!,
            null!,
            null!,
            configuration,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static object CreateRequest(string actionName, params object?[] propertyPairs)
    {
        var requestType = typeof(AuthController)
            .GetMethod(actionName)!
            .GetParameters()[0]
            .ParameterType;

        var request = Activator.CreateInstance(requestType)!;

        for (var i = 0; i < propertyPairs.Length; i += 2)
        {
            var propertyName = (string)propertyPairs[i]!;
            var value = propertyPairs[i + 1];
            requestType.GetProperty(propertyName)!.SetValue(request, value);
        }

        return request;
    }

    private static async Task<IActionResult> Invoke(AuthController controller, string actionName, object request)
    {
        var resultTask = (Task<IActionResult>)typeof(AuthController)
            .GetMethod(actionName)!
            .Invoke(controller, new[] { request })!;

        return await resultTask;
    }
}
