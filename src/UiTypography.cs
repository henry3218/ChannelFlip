using System;
using Microsoft.Win32;
using System.Windows;

namespace ChannelFlip
{
    public static class UiTypography
    {
        // Read the same Windows accessibility setting used by .NET's ScaleHelper.
        // DPI scales the whole window separately; this factor changes text and reflows the layout.
        public static double SystemScale()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility"))
                {
                    object value = key == null ? null : key.GetValue("TextScaleFactor");
                    return value is int && (int)value >= 100 && (int)value <= 225 ? (int)value / 100.0 : 1.0;
                }
            }
            catch (System.Security.SecurityException) { return 1.0; }
            catch (UnauthorizedAccessException) { return 1.0; }
            catch (System.IO.IOException) { return 1.0; }
        }
        public static void Apply(ResourceDictionary resources, double scale)
        {
            if (Double.IsNaN(scale) || scale < 1 || scale > 2.25) throw new ArgumentOutOfRangeException("scale");
            if (resources.Contains("TextScale") && (double)resources["TextScale"] == scale) return;
            resources["TextScale"] = scale;
            foreach (int size in new[] { 13, 14, 16, 17, 25 }) resources["Font" + size] = size * scale;
        }
    }
}
