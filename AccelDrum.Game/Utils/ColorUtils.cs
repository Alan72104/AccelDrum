using OpenTK.Mathematics;
using System;
using System.Drawing;

namespace AccelDrum.Game.Utils;

public static class ColorUtils
{
    /// <summary>
    /// Every value is normalized
    /// </summary>
    public static Vector3 HSLToRGB(Vector3 hsl)
    {
        float h = hsl.X;
        float s = hsl.Y;
        float l = hsl.Z;

        float r, g, b;

        if (s == 0f)
        {
            r = g = b = l; // achromatic
        }
        else
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            r = HueToRGB(p, q, h + 1f / 3f);
            g = HueToRGB(p, q, h);
            b = HueToRGB(p, q, h - 1f / 3f);
        }

        return new Vector3(r, g, b);

        static float HueToRGB(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }

    /// <summary>
    /// Every value is normalized
    /// </summary>
    public static Vector3 RGBToHSL(Vector3 rgb)
    {
        float max = Math.Max(Math.Max(rgb.X, rgb.Y), rgb.Z);
        float min = Math.Min(Math.Min(rgb.X, rgb.Y), rgb.Z);

        float h, s, l;

        // Calculate lightness
        l = (max + min) / 2f;

        if (max == min)
        {
            // Achromatic case (no hue)
            h = 0f;
            s = 0f;
        }
        else
        {
            float delta = max - min;

            // Calculate saturation
            s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

            // Calculate hue
            if (max == rgb.X)
                h = (rgb.Y - rgb.Z) / delta + (rgb.Y < rgb.Z ? 6f : 0f);
            else if (max == rgb.Y)
                h = (rgb.Z - rgb.X) / delta + 2f;
            else
                h = (rgb.X - rgb.Y) / delta + 4f;

            h /= 6f;
        }

        return new Vector3(h, s, l);
    }

    public static uint VectorNetToArgb(System.Numerics.Vector4 v)
    {
        return ((uint)v.W & 0xFF) << 24 |
            ((uint)v.X & 0xFF) << 16 |
            ((uint)v.Y & 0xFF) << 8 |
            ((uint)v.Z % 0xFF);
    }

    public static Vector4 ColorToVector(Color v)
    {
        return new Vector4(
            (v.R / 255.0f),
            (v.G / 255.0f),
            (v.B / 255.0f),
            (v.A / 255.0f));
    }
}
