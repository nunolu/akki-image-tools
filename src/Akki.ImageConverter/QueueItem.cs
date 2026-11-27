using System.ComponentModel;
using System.Runtime.CompilerServices;
using Akki.ImageConverter.Core;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Akki.ImageConverter;

public sealed class QueueItem(ImageSource source) : INotifyPropertyChanged
{
    public ImageSource Source { get; } = source;
    public string Name => System.IO.Path.GetFileName(Source.Path);
    public string Detail => Source.Path;
    private BitmapImage? _thumbnail;
    public BitmapImage Thumbnail => _thumbnail ??= new BitmapImage
    {
        DecodePixelWidth = 160,
        UriSource = new Uri(System.IO.Path.GetFullPath(Source.Path))
    };
    private string _status = "Ready";
    public string Status { get => _status; set { _status = value; Changed(); } }
    public string? OutputPath { get; set; }
    public bool Failed { get; set; }
    public ConversionOptions? VariantOptions { get; set; }
    public long VariantSourceLength { get; set; }
    public DateTime VariantSourceModified { get; set; }
    public Dictionary<uint, ConversionResult> CompletedVariants { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
