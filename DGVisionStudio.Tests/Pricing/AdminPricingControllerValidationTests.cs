using System.Reflection;
using DGVisionStudio.Infrastructure.Controllers;
using FluentAssertions;

namespace DGVisionStudio.Tests.Pricing;

public sealed class AdminPricingControllerValidationTests
{
    [Fact]
    public void Validate_ReturnsError_WhenTitleIsMissing()
    {
        var request = new PricingItemRequest
        {
            Title = " ",
            Description = "Description",
            PricingMode = "Fixed",
            PriceText = "100 EUR"
        };

        var result = InvokeValidate(request);

        result.Should().Be("Заглавието е задължително.");
    }

    [Fact]
    public void Validate_ReturnsError_WhenDescriptionIsMissing()
    {
        var request = new PricingItemRequest
        {
            Title = "Portrait",
            Description = " ",
            PricingMode = "Fixed",
            PriceText = "100 EUR"
        };

        var result = InvokeValidate(request);

        result.Should().Be("Описанието е задължително.");
    }

    [Fact]
    public void Validate_ReturnsError_WhenFixedPriceHasNoPriceText()
    {
        var request = new PricingItemRequest
        {
            Title = "Portrait",
            Description = "Description",
            PricingMode = "Fixed",
            PriceText = " "
        };

        var result = InvokeValidate(request);

        result.Should().Be("Цената е задължителна при фиксирана цена.");
    }

    [Fact]
    public void Validate_ReturnsNull_WhenNegotiablePriceHasNoPriceText()
    {
        var request = new PricingItemRequest
        {
            Title = "Product photography",
            Description = "Description",
            PricingMode = "Negotiable",
            PriceText = " "
        };

        var result = InvokeValidate(request);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Fixed")]
    [InlineData("", "Fixed")]
    [InlineData("fixed", "Fixed")]
    [InlineData("Negotiable", "Negotiable")]
    [InlineData("negotiable", "Negotiable")]
    [InlineData("unknown", "Fixed")]
    public void NormalizePricingMode_NormalizesSupportedValues(string? input, string expected)
    {
        var result = InvokeNormalizePricingMode(input);

        result.Should().Be(expected);
    }

    private static string? InvokeValidate(PricingItemRequest request)
    {
        var method = typeof(AdminPricingController).GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string?)method.Invoke(null, new object[] { request });
    }

    private static string InvokeNormalizePricingMode(string? value)
    {
        var method = typeof(AdminPricingController).GetMethod("NormalizePricingMode", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { value })!;
    }
}
