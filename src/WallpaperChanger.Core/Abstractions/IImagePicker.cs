namespace WallpaperChanger.Core.Abstractions;

public interface IImagePicker
{
    string PeekNext(IReadOnlyCollection<string> imagePaths);

    string PickNext(IReadOnlyCollection<string> imagePaths);

    string? LastPickedImage { get; }

    IReadOnlyList<string> RemainingImages { get; }
}
