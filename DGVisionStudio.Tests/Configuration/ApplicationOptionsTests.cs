using DGVisionStudio.Api.Configuration;
using FluentAssertions;

namespace DGVisionStudio.Tests.Configuration;

public sealed class ApplicationOptionsTests
{
    [Fact]
    public void UploadOptions_MaxRequestSizeBytes_MultipliesFileSizeByFileCount()
    {
        var options = new UploadOptions
        {
            MaxFileSizeBytes = 10,
            MaxFilesPerRequest = 4
        };

        options.MaxRequestSizeBytes.Should().Be(40);
    }

    [Theory]
    [InlineData("Cloudinary", true)]
    [InlineData("cloudinary", true)]
    [InlineData("FileSystem", false)]
    [InlineData(null, false)]
    public void StorageOptions_UseCloudinary_DetectsProvider(string? provider, bool expected)
    {
        var options = new StorageOptions { Provider = provider };

        options.UseCloudinary.Should().Be(expected);
    }

    [Fact]
    public void FrontendOptions_GetAllowedOrigins_TrimsTrailingSlashesAndRemovesDuplicates()
    {
        var options = new FrontendOptions
        {
            Url = "https://dgvisionstudio.com/",
            AdditionalOrigins =
            [
                "https://dgvisionstudio.com",
                "https://admin.dgvisionstudio.com/"
            ]
        };

        var result = options.GetAllowedOrigins();

        result.Should().Equal(
            "https://dgvisionstudio.com",
            "https://admin.dgvisionstudio.com");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://dgvisionstudio.com", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("ftp://dgvisionstudio.com", false)]
    [InlineData("not-a-url", false)]
    public void FrontendOptions_IsValidOrigin_ValidatesHttpAndHttpsOrigins(string? origin, bool expected)
    {
        FrontendOptions.IsValidOrigin(origin).Should().Be(expected);
    }
}
