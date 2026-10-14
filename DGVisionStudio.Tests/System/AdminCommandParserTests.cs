using DGVisionStudio.Api.Services;
using FluentAssertions;

namespace DGVisionStudio.Tests.System;

public sealed class AdminCommandParserTests
{
    [Theory]
    [InlineData("albums.photos.clear-text --all", null)]
    [InlineData("ALBUMS.PHOTOS.CLEAR-TEXT --ALL", null)]
    [InlineData("albums.photos.clear-text --album 12", 12)]
    [InlineData(" albums.photos.clear-text   --album   1 ", 1)]
    public void TryParse_ValidCommands(string input, int? expectedAlbumId)
    {
        AlbumPhotoTextCommand.TryParse(input, out var parsed).Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.AlbumId.Should().Be(expectedAlbumId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("albums.photos.clear-text")]
    [InlineData("albums.photos.clear-text --album 0")]
    [InlineData("albums.photos.clear-text --album -5")]
    [InlineData("albums.photos.clear-text --album abc")]
    [InlineData("albums.photos.clear-text --all --force")]
    [InlineData("DROP TABLE PortfolioImages")]
    [InlineData("albums.photos.clear-text --all; DELETE FROM PortfolioImages")]
    public void TryParse_RejectsUnsupportedOrUnsafeInput(string input)
    {
        AlbumPhotoTextCommand.TryParse(input, out var parsed).Should().BeFalse();
        parsed.Should().BeNull();
    }
}
