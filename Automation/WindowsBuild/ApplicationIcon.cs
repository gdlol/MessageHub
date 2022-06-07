using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KGySoft.Drawing;

namespace WindowsBuild;

public static class ApplicationIcon
{
    private static UIElement LayoutElement(UIElement element, int width, int height)
    {
        var viewbox = new Viewbox
        {
            Child = element
        };
        viewbox.Measure(new Size(width, height));
        viewbox.Arrange(new Rect(0, 0, width, height));
        viewbox.UpdateLayout();
        return viewbox;
    }

    private static byte[] GetPixels(Func<UIElement> elementFactory, int width, int height)
    {
        var element = elementFactory();
        element = LayoutElement(element, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 0, 0, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[4 * width * height];
        bitmap.CopyPixels(pixels, 4 * width, 0);
        return pixels;
    }

    private static System.Drawing.Icon GetIcon(Func<UIElement> elementFactory, int width, int height)
    {
        var pixels = GetPixels(elementFactory, width, height);
        var bitmap = new System.Drawing.Bitmap(width, height);
        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap.ToIcon();
    }

    public static UIElement GetIconElement()
    {
        var fontFamily = new FontFamily("Segoe MDL2 Assets");

        var icon = new Viewbox
        {
            Child = new TextBlock
            {
                Text = "\xE71B",
                FontFamily = fontFamily,
                Foreground = new SolidColorBrush(Color.FromRgb(13, 189, 139))
            },
            Width = 100,
            Height = 100
        };

        return icon;
    }

    public static void SaveIcon(Stream stream)
    {
        var icons = new List<System.Drawing.Icon>();
        try
        {
            var sizeList = new[] { 16, 20, 24, 32, 40, 48, 64, 256 };
            foreach (int size in sizeList)
            {
                var icon = GetIcon(GetIconElement, size, size);
                icons.Add(icon);
            }
            using var combinedIcon = Icons.Combine(icons);
            combinedIcon.Save(stream);
        }
        finally
        {
            foreach (var icon in icons)
            {
                icon.Dispose();
            }
        }
    }
}
