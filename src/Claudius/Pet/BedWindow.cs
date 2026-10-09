using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Claudius.Pet;

/// <summary>
/// The bed standing on the taskbar at night, while the pet is on its way there or has been carried off.
/// Clicks go through it. Once the pet lies in it, the pet window draws the bed around the pet instead.
/// </summary>
public sealed class BedWindow : Window
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, Source = Sprite.RenderBed(BedLayer.Whole) };

    public double Scale { get; }

    public BedWindow(double petScale, bool topmost)
    {
        Scale = petScale;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = topmost;
        Title = "Claudius";
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = _image;
        Width = _image.Width = Sprite.Width * petScale;
        Height = _image.Height = Sprite.Height * petScale;
        Left = -10000;
        Top = -10000;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT) & ~WS_EX_APPWINDOW);
        };
    }

    /// <summary>Stands the bed on the floor at <paramref name="x"/> (its middle), right below the pet in the z-order.</summary>
    public void PlaceAt(double x, double floorY, IntPtr pet)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;
        int left = (int)Math.Round(x - (rect.Right - rect.Left) / 2.0), top = (int)Math.Round(floorY - (rect.Bottom - rect.Top));
        SetWindowPos(hwnd, pet, left, top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
