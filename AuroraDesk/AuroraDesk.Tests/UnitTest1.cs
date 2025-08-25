using AuroraDesk.Models;

namespace AuroraDesk.Tests;

public class WallpaperItemTests
{
    [Fact]
    public void DefaultValues_ShouldBeEmptyStrings()
    {
        var item = new WallpaperItem();

        Assert.Equal(string.Empty, item.Title);
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(string.Empty, item.ThumbnailPath);
        Assert.Equal(string.Empty, item.LaunchPath);
    }

    [Fact]
    public void Properties_SetAndGet_ShouldRoundTrip()
    {
        var item = new WallpaperItem();

        item.Title = "T";
        item.Description = "D";
        item.ThumbnailPath = "thumb.png";
        item.LaunchPath = "c:/a/b/index.html";

        Assert.Equal("T", item.Title);
        Assert.Equal("D", item.Description);
        Assert.Equal("thumb.png", item.ThumbnailPath);
        Assert.Equal("c:/a/b/index.html", item.LaunchPath);
    }
}