using Xunit;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.Core.Tests;

public class ShuffleBagImagePickerTests
{
    [Fact]
    public void Picks_each_image_once_before_repeating()
    {
        var picker = new ShuffleBagImagePicker(new Random(1));
        var images = new[] { "a.jpg", "b.jpg", "c.jpg" };

        var first = picker.PickNext(images);
        var second = picker.PickNext(images);
        var third = picker.PickNext(images);
        var fourth = picker.PickNext(images);

        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
        Assert.Contains(fourth, images);
    }
}
