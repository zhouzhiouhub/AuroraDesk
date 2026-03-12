using AuroraDesk.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuroraDesk.App.ViewModels;

public partial class MonitorDisplayItem : ObservableObject
{
    public MonitorProfile Profile { get; }
    public int Index { get; }
    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
    public double Height { get; }
    public bool IsPrimary => Profile.IsPrimary;
    public string Label => IsPrimary ? $"{Index} ★" : $"{Index}";
    public string Resolution => $"{Profile.Bounds.Width} × {Profile.Bounds.Height}";

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool hasWallpaper;

    public MonitorDisplayItem(
        MonitorProfile profile, int index,
        double left, double top, double width, double height)
    {
        Profile = profile;
        Index = index;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}
