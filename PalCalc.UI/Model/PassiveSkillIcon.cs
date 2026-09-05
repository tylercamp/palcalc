using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Model
{
    internal static class PassiveSkillIcon
    {
        private static Dictionary<int, ImageSource> images;

        public static void Initialize() => _ = Images;

        public static Color ColorForRank(int rank) => rank switch
        {
            < 0 => Color.FromRgb(247, 63, 63),
            4 => Color.FromRgb(104, 255, 216),
            5 => Color.FromRgb(14, 252, 157),
            > 1 => Color.FromRgb(255, 221, 0),
            _ => Color.FromRgb(230, 231, 223),
        };

        public static Dictionary<int, ImageSource> Images
        {
            get
            {
                if (images == null)
                {
                    ImageSource RenderIcon(int rank, string iconName)
                    {
                        using var stream = ResourceLookup.Get($"TraitRank/{iconName}");
                        var mask = new BitmapImage();
                        mask.BeginInit();
                        mask.CacheOption = BitmapCacheOption.OnLoad;
                        mask.StreamSource = stream;
                        mask.EndInit();
                        mask.Freeze();

                        var maskBrush = new ImageBrush(mask);
                        maskBrush.Freeze();
                        var colorBrush = new SolidColorBrush(ColorForRank(rank));
                        colorBrush.Freeze();

                        var visual = new DrawingVisual();
                        using (var drawing = visual.RenderOpen())
                        {
                            drawing.PushOpacityMask(maskBrush);
                            drawing.DrawRectangle(colorBrush, null, new Rect(0, 0, mask.PixelWidth, mask.PixelHeight));
                            drawing.Pop();
                        }

                        var result = new RenderTargetBitmap(mask.PixelWidth, mask.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                        result.Render(visual);
                        result.Freeze();
                        return result;
                    }

                    var rankOne = RenderIcon(1, "Passive_Positive_1_icon.png");
                    images = new Dictionary<int, ImageSource>
                    {
                        [-3] = RenderIcon(-3, "Passive_Negative_3_icon.png"),
                        [-2] = RenderIcon(-2, "Passive_Negative_2_icon.png"),
                        [-1] = RenderIcon(-1, "Passive_Negative_1_icon.png"),
                        [0] = rankOne,
                        [1] = rankOne,
                        [2] = RenderIcon(2, "Passive_Positive_2_icon.png"),
                        [3] = RenderIcon(3, "Passive_Positive_3_icon.png"),
                        [4] = RenderIcon(4, "Passive_Positive_4_icon.png"),
                        [5] = RenderIcon(5, "Passive_Positive_5_icon.png"),
                    };
                }

                return images;
            }
        }
    }
}
