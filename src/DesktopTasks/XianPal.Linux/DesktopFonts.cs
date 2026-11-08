// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Media;

namespace XianPal.Linux;

internal static class DesktopFonts
{
    internal const string TextFamily = "avares://xianpal/Fonts#Noto Sans CJK SC";
    internal const string EmojiFamily = "avares://xianpal/Fonts#Noto Color Emoji";

    internal static AppBuilder WithDesktopFonts(this AppBuilder builder) => builder.With(new FontManagerOptions
    {
        DefaultFamilyName = TextFamily,
        FontFallbacks = new[]
        {
            new FontFallback { FontFamily = new FontFamily(EmojiFamily) },
            new FontFallback { FontFamily = new FontFamily(TextFamily) }
        }
    });
}
