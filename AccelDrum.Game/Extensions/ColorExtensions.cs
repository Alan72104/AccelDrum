using System;
using System.Drawing;

namespace AccelDrum.Game.Extensions;

public static class ColorExtensions
{
    public static Color Darken(this Color color, float percent)
    {
        float factor = 1f - percent;
        int red = (int)(color.R * factor);
        int green = (int)(color.G * factor);
        int blue = (int)(color.B * factor);

        return Color.FromArgb(color.A, red, green, blue);
    }

    public static Color Brighten(this Color color, float percent)
    {
        float factor = 1f + percent;
        int red = Math.Min((int)(color.R * factor), 255);
        int green = Math.Min((int)(color.G * factor), 255);
        int blue = Math.Min((int)(color.B * factor), 255);

        return Color.FromArgb(color.A, red, green, blue);
    }
}
