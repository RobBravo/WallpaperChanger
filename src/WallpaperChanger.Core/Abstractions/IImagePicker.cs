namespace WallpaperChanger.Core.Abstractions;

public interface IImagePicker
{
    string PickNext(IReadOnlyCollection<string> imagePaths);
}
