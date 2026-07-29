using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.Core.Services;

public sealed class ShuffleBagImagePicker : IImagePicker
{
    private readonly Random _random;
    private readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<string> _remaining = new();
    private HashSet<string> _currentSet = new(StringComparer.OrdinalIgnoreCase);

    public ShuffleBagImagePicker(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public string PickNext(IReadOnlyCollection<string> imagePaths)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);

        var uniqueImages = imagePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(_comparer)
            .ToArray();

        if (uniqueImages.Length == 0)
        {
            throw new ArgumentException("At least one image path is required.", nameof(imagePaths));
        }

        var nextSet = new HashSet<string>(uniqueImages, _comparer);
        if (_remaining.Count == 0 || !_currentSet.SetEquals(nextSet))
        {
            Reload(uniqueImages, nextSet);
        }

        var index = _remaining.Count - 1;
        var next = _remaining[index];
        _remaining.RemoveAt(index);
        return next;
    }

    private void Reload(IReadOnlyList<string> uniqueImages, HashSet<string> nextSet)
    {
        _remaining.Clear();
        _remaining.AddRange(uniqueImages);

        for (var i = _remaining.Count - 1; i > 0; i--)
        {
            var swapIndex = _random.Next(i + 1);
            (_remaining[i], _remaining[swapIndex]) = (_remaining[swapIndex], _remaining[i]);
        }

        _currentSet = nextSet;
    }
}
