/*
 * Copyright 2026 Jason Landreth, Open Swing Golf Simulator
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://apache.org
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
 
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices; 
using OpenSwingGolfSimulator.UI.EventArgs; 
using OpenSwingGolfSimulator.Shared.Models;
using OpenSwingGolfSimulator.Physics.Models;

namespace OpenSwingGolfSimulator.UI.Controls
{
    /// <summary>
    /// A WinForms user control that renders an animated 2.5D golf shot tracer over a static
    /// background image, driven entirely by real trajectory sample points supplied in a
    /// <see cref="GolfShotTrajectoryData"/> record. The control does not use any 3D rendering
    /// engine: the illusion of depth comes from projecting the supplied points onto a 2D canvas
    /// toward a fixed vanishing point, and from pairing the airborne tracer arc with a flattened
    /// ground-shadow line. While a shot animates, the control also draws a floating live-yardage
    /// callout that tracks the ball, an apex callout that appears once the ball crests its flight,
    /// and a landing marker that appears once the ball first touches the ground.
    /// </summary>
    [ToolboxItem(true)]
    public partial class GolfShotTracerControl : UserControl
    {
        /// <summary>
        /// The background course image drawn behind the tracer, stretched to fill the control's
        /// client area.
        /// </summary>
        [Category("Appearance")]
        [Description("The background course image drawn behind the tracer, stretched to fill the control.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image BackgroundCourseImage { get; set; }

        /// <summary>
        /// Whether the yardage marker sign images configured in <see cref="_yardageMarkerImages"/>
        /// are drawn along the ground. Unlike the flight arc, shadow, roll line, and callouts,
        /// these markers are part of the range itself rather than the current shot: they are drawn
        /// every frame regardless of whether a shot is in progress, and they never fade out with
        /// <see cref="FadeOutDurationSeconds"/>. Set this to false to hide them entirely (for
        /// example, if your background image already has yardage signage painted into it).
        /// </summary>
        [Category("Yardage Markers")]
        [Description("Whether the yardage marker sign images are drawn along the ground. They are always visible (not tied to the current shot) and never fade out.")]
        [DefaultValue(true)]
        public bool ShowYardageMarkers { get; set; } = true;

        /// <summary>
        /// The on-screen width, in pixels, of a yardage marker sign positioned right at the tee
        /// (zero perspective shrink). Perspective scales every marker's actual drawn width down
        /// from this based on its own downrange distance, the same way it shrinks the flight arc;
        /// see <see cref="DrawYardageMarkers"/>. Each marker's drawn height is derived from this
        /// width using its own image's aspect ratio, so markers of different source dimensions
        /// still line up consistently.
        /// </summary>
        [Category("Yardage Markers")]
        [Description("On-screen width, in pixels, a yardage marker sign would have right at the tee, before perspective shrinks it for its actual downrange distance.")]
        [DefaultValue(160)]
        public int YardageMarkerWidthAtTeePixels { get; set; } = 160;

        /// <summary>
        /// The smallest perspective shrink factor a yardage marker sign is allowed to reach as it
        /// nears the horizon. Without a floor, the same perspective math that shrinks the flight
        /// arc to a point at the horizon would eventually shrink a distant marker (for example, the
        /// 250-yard sign) down to an illegible speck; clamping the shrink factor keeps every
        /// configured marker at least this readable, at the cost of not perfectly matching the
        /// flight arc's own perspective falloff at long distances.
        /// </summary>
        [Category("Yardage Markers")]
        [Description("The smallest perspective shrink factor a yardage marker sign can reach, so distant signs stay legible instead of shrinking to a speck near the horizon.")]
        [DefaultValue(0.18)]
        public double MinimumYardageMarkerShrinkFactor { get; set; } = 0.18;

        /// <summary>
        /// Pairs a downrange distance, in yards, with the yardage marker sign image that should be
        /// drawn at that distance along the ground. See <see cref="_yardageMarkerImages"/> for how
        /// this list is populated and <see cref="DrawYardageMarkers"/> for how each entry is
        /// positioned and scaled.
        /// </summary>
        private readonly struct YardageMarkerImage
        {
            public YardageMarkerImage(double DownrangeYards, Image SourceImage)
            {
                this.DownrangeYards = DownrangeYards;
                this.MarkerImage = NormalizeMarkerImageTransparency(SourceImage);
                this.ContentBounds = ComputeOpaqueContentBounds(this.MarkerImage);
            }

            /// <summary>The downrange distance, in yards, this marker sign should appear at.</summary>
            public double DownrangeYards { get; }

            /// <summary>
            /// The sign image to draw at <see cref="DownrangeYards"/>, after passing through
            /// <see cref="NormalizeMarkerImageTransparency"/>. This is NOT necessarily the same
            /// bitmap the resource file returned: if the source file had no real transparency at
            /// all, this is a corrected copy with its background flood-filled out.
            /// </summary>
            public Bitmap MarkerImage { get; }

            /// <summary>
            /// The tight bounding rectangle, in <see cref="MarkerImage"/>'s own pixel coordinates,
            /// around only its non-transparent content -- computed once here rather than trusting
            /// <see cref="MarkerImage"/>'s full canvas size. Different yardage sign files can (and,
            /// in this project, do) come in with different amounts of surrounding transparent
            /// padding and different overall canvas aspect ratios even when the sign artwork itself
            /// is meant to read as the same physical size; scaling by the raw canvas dimensions
            /// made whichever sign had the least padding relative to its canvas appear larger than
            /// the others at the same configured width. Cropping every marker to this rectangle
            /// before scaling (see <see cref="DrawYardageMarkers"/>) makes all five signs size
            /// consistently regardless of how each source file happens to be padded.
            /// </summary>
            public Rectangle ContentBounds { get; }
        }

        /// <summary>
        /// Returns a version of <paramref name="SourceImage"/> guaranteed to have a real alpha
        /// channel. Of the five yardage marker images actually in use in this project, only two
        /// (the 200 and 250-yard signs) were exported with a genuine transparent background; the
        /// other three are flattened files with their background baked in as solid opaque pixels
        /// (sometimes a plain color, sometimes a transparency-preview checkerboard that got
        /// flattened into the file by whatever tool last touched it). Left alone, those three
        /// would report their ENTIRE canvas as "opaque content" to
        /// <see cref="ComputeOpaqueContentBounds"/>, which is what made them size inconsistently
        /// against the two properly-transparent signs and would also leave a visible rectangular
        /// background patch behind each one when drawn. This method detects which case a given
        /// image is (by checking for any pixel that's already fully transparent) and, for the
        /// flattened case only, estimates the background color from the four corner pixels and
        /// flood fills inward from every border pixel through color-similar neighbors, setting
        /// every reached pixel's alpha to 0. Whatever the flood fill can't reach -- the sign
        /// artwork itself, enclosed on all sides by its own outline -- is left fully opaque.
        /// </summary>
        /// <param name="SourceImage">The original marker image, as loaded from resources.</param>
        /// <returns>
        /// A new <see cref="Bitmap"/> with a real alpha channel: an unmodified copy if
        /// <paramref name="SourceImage"/> already had transparent pixels, or a copy with its
        /// flood-filled background made transparent if it did not.
        /// </returns>
        private static Bitmap NormalizeMarkerImageTransparency(Image SourceImage)
        {
            Bitmap normalizedBitmap = new Bitmap(SourceImage);
            int width = normalizedBitmap.Width;
            int height = normalizedBitmap.Height;

            Rectangle fullBounds = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = normalizedBitmap.LockBits(
                fullBounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            int stride = bitmapData.Stride;
            int byteCount = stride * height;
            byte[] pixelBytes = new byte[byteCount];
            Marshal.Copy(bitmapData.Scan0, pixelBytes, 0, byteCount);

            // If anything in this image is already essentially fully transparent, trust the
            // source's own transparency and leave every pixel exactly as it is.
            const byte FullyTransparentAlphaThreshold = 5;
            bool sourceAlreadyHasRealTransparency = false;

            for (int alphaByteIndex = 3; alphaByteIndex < byteCount; alphaByteIndex += 4)
            {
                if (pixelBytes[alphaByteIndex] <= FullyTransparentAlphaThreshold)
                {
                    sourceAlreadyHasRealTransparency = true;
                    break;
                }
            }

            if (sourceAlreadyHasRealTransparency)
            {
                normalizedBitmap.UnlockBits(bitmapData);
                return normalizedBitmap;
            }

            int GetPixelByteIndex(int PixelX, int PixelY)
            {
                return (PixelY * stride) + (PixelX * 4);
            }

            (byte Red, byte Green, byte Blue) ReadPixelColor(int PixelX, int PixelY)
            {
                int byteIndex = GetPixelByteIndex(PixelX, PixelY);
                return (pixelBytes[byteIndex + 2], pixelBytes[byteIndex + 1], pixelBytes[byteIndex]);
            }

            (int X, int Y)[] cornerPositions =
            {
                (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)
            };

            int cornerRedSum = 0;
            int cornerGreenSum = 0;
            int cornerBlueSum = 0;

            foreach ((int X, int Y) cornerPosition in cornerPositions)
            {
                (byte Red, byte Green, byte Blue) cornerColor =
                    ReadPixelColor(cornerPosition.X, cornerPosition.Y);
                cornerRedSum += cornerColor.Red;
                cornerGreenSum += cornerColor.Green;
                cornerBlueSum += cornerColor.Blue;
            }

            byte estimatedBackgroundRed = (byte)(cornerRedSum / cornerPositions.Length);
            byte estimatedBackgroundGreen = (byte)(cornerGreenSum / cornerPositions.Length);
            byte estimatedBackgroundBlue = (byte)(cornerBlueSum / cornerPositions.Length);

            // How far a pixel's color can be from the estimated background color (per channel)
            // and still count as background. Wide enough to absorb the mild compression noise
            // seen in these flattened exports without being wide enough to eat into the sign's
            // own colors.
            const int BackgroundColorTolerance = 30;

            bool IsBackgroundLikeColor(int PixelX, int PixelY)
            {
                (byte Red, byte Green, byte Blue) pixelColor = ReadPixelColor(PixelX, PixelY);
                return Math.Abs(pixelColor.Red - estimatedBackgroundRed) <= BackgroundColorTolerance
                    && Math.Abs(pixelColor.Green - estimatedBackgroundGreen) <= BackgroundColorTolerance
                    && Math.Abs(pixelColor.Blue - estimatedBackgroundBlue) <= BackgroundColorTolerance;
            }

            bool[,] isBackgroundPixel = new bool[width, height];
            Queue<(int X, int Y)> pixelsToVisit = new Queue<(int X, int Y)>();

            void VisitIfBackgroundLike(int PixelX, int PixelY)
            {
                if (PixelX < 0 || PixelX >= width || PixelY < 0 || PixelY >= height)
                {
                    return;
                }

                if (isBackgroundPixel[PixelX, PixelY])
                {
                    return;
                }

                if (!IsBackgroundLikeColor(PixelX, PixelY))
                {
                    return;
                }

                isBackgroundPixel[PixelX, PixelY] = true;
                pixelsToVisit.Enqueue((PixelX, PixelY));
            }

            for (int x = 0; x < width; x++)
            {
                VisitIfBackgroundLike(x, 0);
                VisitIfBackgroundLike(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                VisitIfBackgroundLike(0, y);
                VisitIfBackgroundLike(width - 1, y);
            }

            while (pixelsToVisit.Count > 0)
            {
                (int X, int Y) currentPixel = pixelsToVisit.Dequeue();

                VisitIfBackgroundLike(currentPixel.X - 1, currentPixel.Y);
                VisitIfBackgroundLike(currentPixel.X + 1, currentPixel.Y);
                VisitIfBackgroundLike(currentPixel.X, currentPixel.Y - 1);
                VisitIfBackgroundLike(currentPixel.X, currentPixel.Y + 1);
            }

            for (int y = 0; y < height; y++)
            {
                int rowStartByteIndex = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int alphaByteIndex = rowStartByteIndex + (x * 4) + 3;
                    pixelBytes[alphaByteIndex] = isBackgroundPixel[x, y] ? (byte)0 : (byte)255;
                }
            }

            Marshal.Copy(pixelBytes, 0, bitmapData.Scan0, byteCount);
            normalizedBitmap.UnlockBits(bitmapData);

            return normalizedBitmap;
        }

        /// <summary>
        /// Scans <paramref name="SourceBitmap"/> for the smallest rectangle, in its own pixel
        /// coordinates, that contains every pixel with non-negligible alpha. Used once per
        /// yardage marker sign, after <see cref="NormalizeMarkerImageTransparency"/> has already
        /// guaranteed a real alpha channel to scan (see
        /// <see cref="YardageMarkerImage.ContentBounds"/>), so each sign is sized and drawn from
        /// its actual visible artwork instead of its full canvas, which may include transparent
        /// padding of a different size and aspect ratio than the other marker images.
        /// </summary>
        /// <param name="SourceBitmap">The image to scan. Not modified.</param>
        /// <returns>
        /// The tight bounding rectangle around non-transparent pixels, or the full image bounds if
        /// the image has no non-transparent pixels at all (so callers always get a valid,
        /// non-empty rectangle to draw from).
        /// </returns>
        private static Rectangle ComputeOpaqueContentBounds(Bitmap SourceBitmap)
        {
            Rectangle fullImageBounds = new Rectangle(0, 0, SourceBitmap.Width, SourceBitmap.Height);

            BitmapData bitmapData = SourceBitmap.LockBits(
                fullImageBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int byteCount = bitmapData.Stride * SourceBitmap.Height;
            byte[] pixelBytes = new byte[byteCount];
            Marshal.Copy(bitmapData.Scan0, pixelBytes, 0, byteCount);
            SourceBitmap.UnlockBits(bitmapData);

            // Pixels with alpha at or below this are treated as "not really part of the
            // sign" -- filters out faint anti-aliasing dust at the extreme edge of the
            // canvas without needing every pixel to be perfectly, fully transparent.
            const byte MinimumOpaqueAlpha = 10;

            int leftmostOpaqueX = SourceBitmap.Width;
            int rightmostOpaqueX = -1;
            int topmostOpaqueY = SourceBitmap.Height;
            int bottommostOpaqueY = -1;

            for (int y = 0; y < SourceBitmap.Height; y++)
            {
                int rowStartByteIndex = y * bitmapData.Stride;

                for (int x = 0; x < SourceBitmap.Width; x++)
                {
                    byte pixelAlpha = pixelBytes[rowStartByteIndex + (x * 4) + 3];

                    if (pixelAlpha <= MinimumOpaqueAlpha)
                    {
                        continue;
                    }

                    if (x < leftmostOpaqueX)
                    {
                        leftmostOpaqueX = x;
                    }

                    if (x > rightmostOpaqueX)
                    {
                        rightmostOpaqueX = x;
                    }

                    if (y < topmostOpaqueY)
                    {
                        topmostOpaqueY = y;
                    }

                    if (y > bottommostOpaqueY)
                    {
                        bottommostOpaqueY = y;
                    }
                }
            }

            bool foundAnyOpaquePixel = rightmostOpaqueX >= leftmostOpaqueX
                && bottommostOpaqueY >= topmostOpaqueY;

            if (!foundAnyOpaquePixel)
            {
                return fullImageBounds;
            }

            return Rectangle.FromLTRB(leftmostOpaqueX, topmostOpaqueY, rightmostOpaqueX + 1, bottommostOpaqueY + 1);
        }

        /// <summary>
        /// The fixed set of yardage marker signs drawn along the ground, one per configured
        /// downrange distance. Populated once from the project's embedded image resources (see the
        /// Properties.Resources.* references below); rename these to match whatever names Visual
        /// Studio assigned your images when you added them under Project Properties &gt; Resources
        /// (or Solution Explorer &gt; Properties &gt; Resources.resx), and adjust the yardages if
        /// your signs use different distances.
        /// </summary>
        private readonly List<YardageMarkerImage> _yardageMarkerImages = new List<YardageMarkerImage>
        {

            new YardageMarkerImage(50, Properties.Resources.yardage_marker_50),
            new YardageMarkerImage(100, Properties.Resources.yardage_marker_100),
            new YardageMarkerImage(150, Properties.Resources.yardage_marker_150),
            new YardageMarkerImage(200, Properties.Resources.yardage_marker_200),
            new YardageMarkerImage(250, Properties.Resources.yardage_marker_250),
        };

        /// <summary>
        /// The screen-relative position, expressed as a fraction of the control's width and
        /// height (each in the range 0 to 1), where the ball is teed up. Defaults to horizontally
        /// centered and near the bottom of the control.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is one of the two properties you adjust when the drawn tracer doesn't line up
        /// with your background image (the other is <see cref="HorizonPositionFraction"/>).
        /// <see cref="TeeOffPositionFraction"/> answers the question "where on screen does the
        /// ball start?"
        /// </para>
        /// <para>
        /// Both X and Y are fractions of the control's own size, not pixel coordinates, so the
        /// alignment holds up if the control is resized or the form is resized:
        /// <list type="bullet">
        /// <item><description>X = 0.0 is the control's left edge, X = 1.0 is the right edge, X = 0.5 is horizontally centered.</description></item>
        /// <item><description>Y = 0.0 is the control's top edge, Y = 1.0 is the bottom edge, Y = 0.86 is 86% of the way down.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// How to find the right values: put a background image on the control (set
        /// <see cref="BackgroundCourseImage"/>), run a sample shot from
        /// <c>SampleShotDataGenerator</c>, and watch where the white ball marker starts relative
        /// to the tee marker or hitting mat drawn in your image. Then nudge the values:
        /// <list type="bullet">
        /// <item><description>Ball starts too high above the mat in your image → increase Y (move it toward 1.0, closer to the bottom).</description></item>
        /// <item><description>Ball starts below/off the bottom of your image → decrease Y (move it toward 0.0).</description></item>
        /// <item><description>Ball starts too far right of the mat → decrease X (move it toward 0.0, the left edge).</description></item>
        /// <item><description>Ball starts too far left of the mat → increase X (move it toward 1.0, the right edge).</description></item>
        /// </list>
        /// </para>
        /// <example>
        /// Example: your background image shows the hitting mat centered horizontally but sitting
        /// very close to the bottom edge of the photo, lower than the default 86% assumes. In the
        /// designer's Properties window, expand <c>TeeOffPositionFraction</c> and change Y from
        /// 0.86 to 0.93 (leave X at 0.5 since the mat is still centered). Equivalently, in code:
        /// <code>
        /// tracerControl.TeeOffPositionFraction = new PointF(0.5F, 0.88F);
        /// </code>
        /// If your camera angle is instead offset to the left of center (common if the sim's
        /// camera isn't perfectly centered over the mat), you would also shift X, for example:
        /// <code>
        /// tracerControl.TeeOffPositionFraction = new PointF(0.5F, 0.88F);
        /// </code>
        /// </example>
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Where the ball is teed up on screen, as a fraction of the control's width and height (0,0 = top-left, 1,1 = bottom-right). Adjust this first if the tracer's starting point doesn't line up with your background image.")]
        [DefaultValue(typeof(PointF), "0.5F, 0.93F")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public PointF TeeOffPositionFraction { get; set; } = new PointF(0.5F, 0.93F);

        /// <summary>
        /// The screen-relative position, expressed as a fraction of the control's width and
        /// height (each in the range 0 to 1), of the horizon / vanishing point that the flight
        /// path converges toward as downrange distance increases. Set this to match the tree
        /// line or horizon in your background image.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the second of the two properties you adjust for alignment (the first is
        /// <see cref="TeeOffPositionFraction"/>). Where the tee position answers "where does the
        /// ball start?", <see cref="HorizonPositionFraction"/> answers "where does the flight path
        /// disappear into the distance?" Every trajectory point is interpolated between the tee
        /// position and this horizon position, so this property controls how tall and how curved
        /// the whole arc looks, not just its endpoint.
        /// </para>
        /// <para>
        /// Same fraction system as <see cref="TeeOffPositionFraction"/>: X = 0 is the left edge, X
        /// = 1 is the right edge; Y = 0 is the top edge, Y = 1 is the bottom edge. The default,
        /// 0.5, 0.4, targets a point horizontally centered and 40% of the way down the control —
        /// roughly where a tree line sits in a typical range photo shot from behind the hitting
        /// bay.
        /// </para>
        /// <para>
        /// How to find the right values:
        /// <list type="bullet">
        /// <item><description>Arc peaks above the tree line / looks too tall → decrease Y slightly (move the horizon up, toward 0.0) so the arc has less vertical room to work with, or lower <see cref="HeightFeetToPixelsScale"/> instead if the horizon position itself already looks right.</description></item>
        /// <item><description>Arc looks flattened into the ground / too short → increase Y (move the horizon down, toward 1.0).</description></item>
        /// <item><description>Long shots visually run off the right or left edge of the image before finishing → recenter X, or reduce <see cref="LateralYardsToPixelsScale"/> so lateral drift is less exaggerated.</description></item>
        /// <item><description>Far shots look like they curve toward the wrong side of the fairway → check that X matches the true center of your fairway in the image, not just the center of the control.</description></item>
        /// </list>
        /// </para>
        /// <example>
        /// Example: your background image's visible tree line sits noticeably higher in the frame
        /// than the default assumes (closer to the top third of the photo), and the fairway's
        /// vanishing point is a little left of the control's horizontal center because of how the
        /// image was cropped. In the designer, expand <c>HorizonPositionFraction</c> and set X to
        /// 0.47 and Y to 0.33. Equivalently, in code:
        /// <code>
        /// tracerControl.HorizonPositionFraction = new PointF(0.5F, 0.15F);
        /// </code>
        /// </example>
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Where the flight path converges to in the distance, as a fraction of the control's width and height. Match this to the tree line / horizon in your background image; it controls how tall and how curved the whole arc looks.")]
        [DefaultValue(typeof(PointF), "0.5F, 0.15F")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public PointF HorizonPositionFraction { get; set; } = new PointF(0.5F, 0.15F);

        /// <summary>
        /// The total length, in yards, of the range being represented on screen: the downrange
        /// distance at which a shot is drawn exactly at the horizon. Every trajectory point's
        /// on-screen depth is simply <c>Min(DownrangeDistanceYards / LongestExpectedShotYards,
        /// 1.0)</c> — a straight linear ramp from the tee (0 yards, depth 0) to this distance
        /// (depth 1, right at the horizon), clamped so a shot longer than this still lands exactly
        /// at the horizon rather than overshooting it. Defaults to 300.0 yards, representing a
        /// range that runs 300 yards from the tee to its far end.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This replaces an earlier asymptotic easing curve
        /// (<c>Y / (Y + SomeConstant)</c>) that was hard to predict: because that curve never
        /// actually reaches the horizon at any finite distance, picking its tuning constant
        /// without doing the exact arithmetic for your own shot distances could easily leave
        /// realistic shots using only a small fraction of the available screen depth — which is
        /// what made a real 160-yard shot look like it had barely left the tee. This linear model
        /// removes that guesswork entirely: whatever you set <see cref="LongestExpectedShotYards"/>
        /// to is, by construction, exactly where the horizon is, and every other distance is a
        /// simple, predictable fraction of that.
        /// </para>
        /// <para>
        /// <b>Do not set this lower than the range you actually want represented</b> (for example,
        /// setting it to a small value like 10 while your shots travel 150-250+ yards). Once a
        /// trajectory point's downrange distance passes this value, its depth is clamped to
        /// exactly 1.0 and the perspective "shrink" factor becomes exactly zero for every point
        /// beyond that — meaning every remaining point of the shot, no matter how much farther it
        /// actually travels, is drawn at the exact same horizon pixel. Visually this looks like the
        /// tracer line stops advancing partway through the animation (it isn't a fade or an
        /// animation bug — there is genuinely no more screen distance left to draw the rest of the
        /// flight against). Always set this to at least your longest realistic total shot distance
        /// (carry plus rollout).
        /// </para>
        /// <example>
        /// With the default of 300 yards, a 218-yard shot reaches <c>218 / 300 ≈ 73%</c> of the
        /// way to the horizon, and a 250-yard drive reaches <c>250 / 300 ≈ 83%</c>:
        /// <code>
        /// tracerControl.LongestExpectedShotYards = 300.0;
        /// </code>
        /// </example>
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("The total length, in yards, of the range being represented on screen — the downrange distance at which a shot reaches exactly the horizon. Every other distance is drawn as a straight linear fraction of this. Never set it below your longest realistic shot distance.")]
        [DefaultValue(300D)]
        public double LongestExpectedShotYards { get; set; } = 300D;

        /// <summary>
        /// A tuning value, in pixels per yard, that controls how wide the flight path's lateral
        /// (left/right) curve appears at the tee, before perspective shrinks it with distance.
        /// </summary>
        /// <remarks>
        /// Raised from an earlier default of 6.5 to 16.0: at 6.5, even a shot with several yards
        /// of real curve only drew a few pixels of visible lateral drift near the tee (where
        /// <c>perspectiveShrinkFactor</c> is close to 1 and the effect is least diminished),
        /// making every shot's flight path look nearly dead straight and short regardless of its
        /// actual shape. If your shots still look too narrow or too wide after this change, this
        /// is the value to keep adjusting — it scales linearly, so doubling it doubles the visible
        /// lateral spread at every point along the flight.
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Pixels per yard of lateral (left/right) offset at the tee, before perspective shrinks it with distance.")]
        [DefaultValue(16.0)]
        public double LateralYardsToPixelsScale { get; set; } = 16.0;

        /// <summary>
        /// A tuning value, in pixels per foot, that controls how tall the flight arc appears at
        /// the tee, before perspective shrinks it with distance. Feet, not yards, to match the
        /// height unit used by <see cref="TrajectoryPoint3D.Z"/>.
        /// </summary>
        /// <remarks>
        /// Raised in stages from an original default of 1.85 (too flat) through 4.5 and 10.0 to
        /// 18.0: at 1.85, a typical short shot's apex (a few dozen feet) only produced a few dozen
        /// pixels of visible arc height, making every shot look flat and short regardless of its
        /// actual trajectory. If shots still look too flat or too tall at 18.0, this is the value
        /// to keep adjusting — it scales linearly, so doubling it doubles the visible arc height
        /// at every point along the flight.
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Pixels per foot of height at the tee, before perspective shrinks it with distance. Uses feet to match TrajectoryPoint3D.Z.")]
        [DefaultValue(18.0)]
        public double HeightFeetToPixelsScale { get; set; } = 18.0;

        /// <summary>
        /// The total wall-clock duration, in seconds, that the shot animation takes to play from
        /// the tee to the ball coming to rest, regardless of the shot's actual distance. This is
        /// the control's "line drawing speed" — lower values draw the tracer faster.
        /// </summary>
        [Category("Animation")]
        [Description("Total time, in seconds, for the tracer to animate from tee to final rest. Lower values draw the line faster.")]
        [DefaultValue(2.6)]
        public double AnimationDurationSeconds { get; set; } = 2.6;

        /// <summary>
        /// The fraction of the total animation duration (0 to 1) allotted to the airborne flight
        /// phase. The remainder is allotted to the ground-roll phase.
        /// </summary>
        [Category("Animation")]
        [Description("Fraction (0 to 1) of the total animation time spent on the airborne flight phase; the rest is spent on rollout.")]
        [DefaultValue(0.72)]
        public double AnimationFlightPhaseShare { get; set; } = 0.72;

        [Category("Trajectory")]
        [Description("Controls the visual amount of left or right curvature.")]
        [DefaultValue(4.5)]
        public double CurveMultiplier { get; set; } = 4.5;

        /// <summary>
        /// The wall-clock duration, in seconds, over which a completed shot's tracer, shadow,
        /// roll line, ball marker, and live-yardage callout fade to fully transparent after the
        /// ball comes to rest, before the control clears itself and is ready for the next shot.
        /// Set to 0 to skip fading and hold the finished shot on screen indefinitely instead
        /// (matching the control's previous behavior), in which case you must call
        /// <see cref="ResetShot"/> yourself before starting the next shot.
        /// </summary>
        /// <remarks>
        /// Without a fade-out, a finished shot's tracer simply stays frozen on screen forever —
        /// nothing clears it until something explicitly calls <see cref="ResetShot"/> or
        /// <see cref="StartShot(GolfShotTrajectoryData)"/> again. If your shot source fires shots
        /// one after another (for example, waiting for the control to visually finish with one
        /// shot before it feels right to show the next), that stuck frozen tracer is what makes it
        /// look like the control "never moves on" to the next shot. Enabling the fade-out (the
        /// default) makes the control clean itself up on its own a beat after each shot ends.
        /// </remarks>
        [Category("Animation")]
        [Description("Seconds for a completed shot to fade out before the control auto-clears itself. Set to 0 to hold the finished shot on screen indefinitely instead (call ResetShot yourself in that case).")]
        [DefaultValue(5.0)]
        public double FadeOutDurationSeconds { get; set; } = 5.0;

        /// <summary>
        /// The color used to draw the airborne portion of the tracer arc.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the airborne tracer line.")]
        [DefaultValue(typeof(Color), "Red")]
        public Color FlightArcColor { get; set; } = Color.Red;

        /// <summary>
        /// The color used to draw the ground shadow beneath the airborne tracer arc. Intended to
        /// read as a soft, translucent dark shadow darkening the background image beneath it —
        /// not as a second colored line — so this should always be a low-alpha near-black/gray,
        /// never an opaque color.
        /// </summary>
        /// <remarks>
        /// The previous default, <c>Color.FromArgb(160, 40, 20)</c>, used the three-argument
        /// <see cref="Color.FromArgb(int, int, int)"/> overload (red, green, blue — no alpha), which
        /// is fully opaque and reads as R=160, G=40, B=20: a solid brick-red/brown line, not a
        /// shadow at all. The corrected default below uses the four-argument
        /// <see cref="Color.FromArgb(int, int, int, int)"/> overload (alpha, red, green, blue) with
        /// a low alpha over pure black, so it darkens whatever's underneath instead of painting a
        /// colored line on top of it.
        /// </remarks>
        [Category("Appearance")]
        [Description("The color of the ground shadow line drawn beneath the airborne tracer. Should be a low-alpha near-black/gray so it reads as a shadow, not a colored line.")]
        [DefaultValue(typeof(Color), "100, 0, 0, 0")]
        public Color GroundShadowColor { get; set; } = Color.FromArgb(100, 0, 0, 0);

        /// <summary>
        /// The color used to draw the ground-roll segment after the ball lands.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the ground-roll line drawn after the ball lands.")]
        [DefaultValue(typeof(Color), "200, 120, 40")]
        public Color RollSegmentColor { get; set; } = Color.FromArgb(200, 120, 40);

        /// <summary>
        /// The color used to draw the circular marker at the point where the ball first touches
        /// the ground.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the ring marker drawn where the ball first touches the ground.")]
        [DefaultValue(typeof(Color), "Red")]
        public Color LandingMarkerColor { get; set; } = Color.Red;

        /// <summary>
        /// The fill color of the floating callout box that shows the live downrange yardage as
        /// the ball flies and rolls.
        /// </summary>
        [Category("Appearance")]
        [Description("The fill color of the live-yardage callout box that follows the ball.")]
        [DefaultValue(typeof(Color), "230, 20, 20, 20")]
        public Color LiveYardageBoxColor { get; set; } = Color.FromArgb(230, 20, 20, 20);

        /// <summary>
        /// The font used to render the live-yardage callout.
        /// </summary>
        [Category("Appearance")]
        [Description("The font used for the live-yardage callout text.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Font CalloutFont { get; set; } = new Font("Segoe UI", 11f, FontStyle.Bold);

        /// <summary>
        /// An exponent applied to the linear tee-to-horizon depth fraction
        /// (<c>DownrangeDistanceYards / LongestExpectedShotYards</c>) before it is used to
        /// interpolate a trajectory point's screen position: <c>normalizedDepth =
        /// Math.Pow(linearDepth, PerspectivePower)</c>. Because <c>linearDepth</c> is always
        /// between 0 and 1, an exponent below 1.0 pushes <c>normalizedDepth</c> above
        /// <c>linearDepth</c> at every point along the flight (a fractional power of a fraction is
        /// larger than the fraction itself), which is what makes shots travel farther across the
        /// screen than the plain linear model alone would — the effect is strongest for short
        /// shots, since raising a very small fraction to a power below 1 boosts it proportionally
        /// more than a fraction already close to 1. An exponent of exactly 1.0 reproduces the
        /// plain linear model with no added boost; an exponent above 1.0 would instead compress
        /// <c>normalizedDepth</c> below <c>linearDepth</c>, making shots look like they travel less
        /// far, not more.
        /// </summary>
        /// <remarks>
        /// Lowered from 0.3 to 0.18: with <see cref="LongestExpectedShotYards"/> at its own default
        /// of 300, a value of 0.3 put 50 downrange yards at 58% of the way from tee to horizon --
        /// so close to the tee visually that the 50-yard mark (and, since
        /// <see cref="DrawYardageMarkers"/> shares this exact formula, a real 50-yard shot too)
        /// looked like it landed right on top of the hitting mat instead of noticeably downrange
        /// of it. 0.18 was picked so that 50 yards now lands at roughly the same on-screen depth
        /// that 100 yards used to at 0.3 (about 72% of the way to the horizon), pushing every
        /// distance farther from the tee and giving short shots more visible room to travel. This
        /// is a starting point derived from the math, not something verified against your actual
        /// background image -- watch how a 50-yard shot lines up against your own art and nudge
        /// this (or <see cref="LongestExpectedShotYards"/>) further if it's still not far enough,
        /// or too far now.
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Controls how downrange distance is visually distributed across the perspective image. Lower values push shots farther downrange; higher values compress them toward the horizon.")]
        [DefaultValue(0.115)]
        public double PerspectivePower { get; set; } = 0.115;

        /// <summary>
        /// A tuning value, from 0 to 1, that flattens the landing marker ring drawn at the point
        /// where the ball first touches the ground, so it reads as an ellipse lying flat on the
        /// ground rather than a perfect circle floating in space. A value of 1.0 draws a full
        /// circle; smaller values compress the ring's vertical (downrange) axis relative to its
        /// horizontal (lateral) axis, matching how a circle drawn flat on the ground would
        /// foreshorten from this control's elevated, angled camera perspective.
        /// </summary>
        [Category("Landing Marker")]
        [Description("Controls how flat the landing marker appears on the ground. Lower values make the marker flatter.")]
        [DefaultValue(0.3)]
        public double LandingMarkerDepthScale { get; set; } = 0.3;
        /// <summary>
        /// Raised once per shot, the moment the ball comes to rest at the shot's total distance
        /// (before any fade-out begins).
        /// </summary>
        public event EventHandler ShotAnimationCompleted;

        /// <summary>
        /// Raised once per shot, after the completed shot has finished fading out and the control
        /// has cleared itself and is ready for <see cref="StartShot(GolfShotTrajectoryData)"/> to
        /// be called again. Only raised when <see cref="FadeOutDurationSeconds"/> is greater than
        /// 0; with fading disabled, use <see cref="ShotAnimationCompleted"/> instead and call
        /// <see cref="ResetShot"/> yourself when you're ready to clear the control.
        /// </summary>
        public event EventHandler ShotFadeOutCompleted;

        /// <summary>
        /// The shot currently being animated or faded out, or null if no shot has been started
        /// yet (or the previous shot has finished fading out).
        /// </summary>
        private GolfShotTrajectoryData _currentShotData;

        /// <summary>
        /// The current animation progress, expressed as a fraction from 0 (tee) to 1 (ball at
        /// rest). Advanced once per animation timer tick.
        /// </summary>
        private double _currentAnimationProgress;

        /// <summary>
        /// True while a shot animation is actively progressing (before the ball has come to rest).
        /// </summary>
        private bool _isAnimationInProgress;

        /// <summary>
        /// True while a completed shot is fading out, after the flight/rollout animation has
        /// finished but before the control has cleared itself.
        /// </summary>
        private bool _isFadingOut;

        /// <summary>
        /// The elapsed time, in seconds, since the current fade-out began. Only meaningful while
        /// <see cref="_isFadingOut"/> is true.
        /// </summary>
        private double _fadeOutElapsedSeconds;

        /// <summary>
        /// Initializes a new instance of the tracer control, wiring up its designer-generated
        /// components.
        /// </summary>
        public GolfShotTracerControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Begins animating a new golf shot tracer from real trajectory sample points, replacing
        /// any shot that is currently animating.
        /// </summary>
        /// <param name="ShotData">
        /// The trajectory points and summary stats describing the shot to animate.
        /// <see cref="GolfShotTrajectoryData.CarryTrajectoryPoints"/> must contain at least two
        /// points, and <see cref="GolfShotTrajectoryData.RolloutTrajectoryPoints"/> must not be
        /// null (pass an empty list for a shot with no rollout).
        /// </param>
        public void StartShot(GolfShotTrajectoryData ShotData)
        {
            if (ShotData == null)
            {
                throw new ArgumentNullException(nameof(ShotData));
            }

            if (ShotData.CarryTrajectoryPoints == null || ShotData.CarryTrajectoryPoints.Count < 2)
            {
                throw new ArgumentException(
                    "CarryTrajectoryPoints must contain at least two points.", nameof(ShotData));
            }

            if (ShotData.RolloutTrajectoryPoints == null)
            {
                throw new ArgumentException(
                    "RolloutTrajectoryPoints must not be null; pass an empty list for no rollout.",
                    nameof(ShotData));
            }

            _currentShotData = ShotData;
            _currentAnimationProgress = 0.0;
            _isAnimationInProgress = true;
            _isFadingOut = false;
            _fadeOutElapsedSeconds = 0.0;
            AnimationRefreshTimer.Start();
            Invalidate();
        }

        /// <summary>
        /// An <see cref="EventHandler{ShotRecordedEventArgs}"/>-compatible method that starts
        /// animating the shot carried by <paramref name="ShotEventArgs"/>. Exists so this control
        /// can be wired directly to a parent-level shot-recorded event without a wrapping lambda —
        /// for example: <c>someShotSource.OnShotRecorded += tracerControl.OnShotRecorded;</c>.
        /// Internally this simply forwards to <see cref="StartShot(GolfShotTrajectoryData)"/>.
        /// </summary>
        /// <param name="EventSender">The source of the event (unused).</param>
        /// <param name="ShotEventArgs">The event args carrying the newly recorded shot.</param>
        public void OnShotRecorded(object EventSender, ShotRecordedEventArgs ShotEventArgs)
        {
            if (ShotEventArgs == null)
            {
                throw new ArgumentNullException(nameof(ShotEventArgs));
            }

            StartShot(ShotEventArgs.ShotRecord);
        }

        /// <summary>
        /// Immediately stops any in-progress animation and clears the tracer from the control, so
        /// the next repaint shows only the background image.
        /// </summary>
        public void ResetShot()
        {
            AnimationRefreshTimer.Stop();
            _currentShotData = null;
            _currentAnimationProgress = 0.0;
            _isAnimationInProgress = false;
            _isFadingOut = false;
            _fadeOutElapsedSeconds = 0.0;
            Invalidate();
        }

        /// <summary>
        /// Handles each tick of the animation timer. While a shot is actively animating, this
        /// advances the animation progress fraction according to the elapsed timer interval and
        /// <see cref="AnimationDurationSeconds"/>. Once the ball comes to rest,
        /// <see cref="ShotAnimationCompleted"/> is raised and, if <see cref="FadeOutDurationSeconds"/>
        /// is greater than 0, the control moves into a fade-out phase instead of stopping outright —
        /// advancing <see cref="_fadeOutElapsedSeconds"/> on each subsequent tick until the fade
        /// completes, at which point the control clears itself via <see cref="ResetShot"/> and
        /// raises <see cref="ShotFadeOutCompleted"/>. This is what lets the control clean up after
        /// itself and be ready for the next shot without the caller having to call
        /// <see cref="ResetShot"/> manually.
        /// </summary>
        /// <param name="SenderTimer">The animation timer that raised the event.</param>
        /// <param name="TickEventArgs">The event arguments for the timer tick (unused).</param>
        private void AnimationRefreshTimer_Tick(object SenderTimer, System.EventArgs TickEventArgs)
        {
            if (!_isAnimationInProgress && !_isFadingOut)
            {
                AnimationRefreshTimer.Stop();
                return;
            }

            double secondsPerTick = AnimationRefreshTimer.Interval / 1000.0;

            if (_isAnimationInProgress)
            {
                double progressIncrement = secondsPerTick / Math.Max(AnimationDurationSeconds, 0.001);
                _currentAnimationProgress += progressIncrement;

                if (_currentAnimationProgress >= 1.0)
                {
                    _currentAnimationProgress = 1.0;
                    _isAnimationInProgress = false;
                    ShotAnimationCompleted?.Invoke(this, System.EventArgs.Empty);

                    if (FadeOutDurationSeconds > 0.0)
                    {
                        _isFadingOut = true;
                        _fadeOutElapsedSeconds = 0.0;
                    }
                    else
                    {
                        AnimationRefreshTimer.Stop();
                    }
                }

                Invalidate();
                return;
            }

            _fadeOutElapsedSeconds += secondsPerTick;

            if (_fadeOutElapsedSeconds >= FadeOutDurationSeconds)
            {
                ResetShot();
                ShotFadeOutCompleted?.Invoke(this, System.EventArgs.Empty);
                return;
            }

            Invalidate();
        }

        /// <summary>
        /// Computes how opaque the tracer, shadow, ball marker, and live-yardage callout should be
        /// drawn for the current frame: fully opaque (1.0) whenever a shot is animating or no fade
        /// is in progress, tapering linearly down to fully transparent (0.0) over the course of a
        /// fade-out.
        /// </summary>
        /// <returns>A value from 0.0 (fully faded) to 1.0 (fully opaque).</returns>
        private float GetCurrentFadeAlphaMultiplier()
        {
            if (!_isFadingOut)
            {
                return 1f;
            }

            double fadeFraction = _fadeOutElapsedSeconds / Math.Max(FadeOutDurationSeconds, 0.001);
            return (float)Math.Max(0.0, 1.0 - fadeFraction);
        }

        /// <summary>
        /// Returns <paramref name="BaseColor"/> with its alpha scaled by
        /// <paramref name="AlphaMultiplier"/>, used to fade every element of the tracer out
        /// together at the same rate during a shot's fade-out phase.
        /// </summary>
        /// <param name="BaseColor">The element's normal, fully-opaque-intent color.</param>
        /// <param name="AlphaMultiplier">
        /// A value from 0.0 (fully transparent) to 1.0 (<paramref name="BaseColor"/>'s own alpha,
        /// unchanged).
        /// </param>
        /// <returns>The faded color.</returns>
        private Color ApplyFadeAlpha(Color BaseColor, float AlphaMultiplier)
        {
            int scaledAlpha = (int)Math.Round(BaseColor.A * AlphaMultiplier);
            return Color.FromArgb(scaledAlpha, BaseColor.R, BaseColor.G, BaseColor.B);
        }

        /// <summary>
        /// Projects a trajectory sample point onto a 2D screen-space point using a simple
        /// perspective model: downrange distance (<see cref="TrajectoryPoint3D.Y"/>) is mapped
        /// linearly toward the horizon using <see cref="LongestExpectedShotYards"/>, and both the
        /// lateral offset (<see cref="TrajectoryPoint3D.X"/>) and height
        /// (<see cref="TrajectoryPoint3D.Z"/>) are scaled down as the point recedes into the
        /// distance, so farther points appear smaller and closer to the centerline. This is the
        /// core of the "2.5D" illusion — no true 3D geometry is involved, only 2D interpolation
        /// and scaling.
        /// </summary>
        /// <param name="TrajectoryPoint">The trajectory sample point to project.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        /// <returns>The corresponding point in control-relative screen coordinates.</returns>
        private PointF ProjectTrajectoryPointToScreen(Vector3D TrajectoryPoint, Rectangle ControlBounds)
        {
            PointF teeScreenPosition = new PointF(
                ControlBounds.Width * TeeOffPositionFraction.X,
                ControlBounds.Height * TeeOffPositionFraction.Y);

            PointF horizonScreenPosition = new PointF(
                ControlBounds.Width * HorizonPositionFraction.X,
                ControlBounds.Height * HorizonPositionFraction.Y);

            double downrangeDistanceYards = TrajectoryPoint.Y;
            double lateralOffsetYards = TrajectoryPoint.X;
            double heightFeet = TrajectoryPoint.Z;

            double linearDepth = Math.Min(
                downrangeDistanceYards / Math.Max(LongestExpectedShotYards, 1.0), 1.0);

            // Apply perspective compression.
            // Values closer to 0 stay spread out farther down the screen,
            // while values closer to 1 compress toward the vanishing point. 

            double normalizedDepth = Math.Pow(linearDepth, PerspectivePower);

            float baseScreenPositionX = teeScreenPosition.X
                + ((horizonScreenPosition.X - teeScreenPosition.X) * (float)normalizedDepth);

            float baseScreenPositionY = teeScreenPosition.Y
                + ((horizonScreenPosition.Y - teeScreenPosition.Y) * (float)normalizedDepth);

            double perspectiveShrinkFactor = 1.0 - normalizedDepth;

            float lateralScreenOffset = (float)(lateralOffsetYards
                * LateralYardsToPixelsScale * perspectiveShrinkFactor);

            float heightScreenOffset = (float)(heightFeet
                * HeightFeetToPixelsScale * perspectiveShrinkFactor);

            return new PointF(
                baseScreenPositionX + lateralScreenOffset,
                baseScreenPositionY - heightScreenOffset);
        }

        /// <summary>
        /// Scans the carry trajectory for the index of its highest point (greatest
        /// <see cref="TrajectoryPoint3D.Z"/> value), used to place the apex marker exactly where
        /// the supplied data actually peaks.
        /// </summary>
        /// <param name="CarryPoints">The airborne trajectory points to scan.</param>
        /// <returns>
        /// The index of the highest point, or -1 if <paramref name="CarryPoints"/> is null or
        /// empty.
        /// </returns>
        private int FindApexPointIndex(List<Vector3D> CarryPoints)
        {
            if (CarryPoints == null || CarryPoints.Count == 0)
            {
                return -1;
            }

            int highestPointIndex = 0;
            double highestHeightSoFar = CarryPoints[0].Z;

            for (int pointIndex = 1; pointIndex < CarryPoints.Count; pointIndex++)
            {
                if (CarryPoints[pointIndex].Z > highestHeightSoFar)
                {
                    highestHeightSoFar = CarryPoints[pointIndex].Z;
                    highestPointIndex = pointIndex;
                }
            }

            return highestPointIndex;
        }

        /// <summary>
        /// Builds the complete screen-space geometry for the current animation frame by walking
        /// the shot's real carry and rollout trajectory points up to the current animation
        /// progress: the flight arc drawn so far, the matching ground shadow, the roll segment
        /// (once the ball has landed), the ball's current position, the apex marker position once
        /// the ball has passed its highest point, and the landing marker position once the ball
        /// has completed its flight phase.
        /// </summary>
        /// <param name="ShotData">The shot currently being animated.</param>
        /// <param name="AnimationProgress">The current animation progress, from 0 to 1.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        /// <returns>The screen-space geometry and readout values for this frame.</returns>
        private TracerFrameGeometry BuildFrameGeometry(GolfShotTrajectoryData ShotData, double AnimationProgress, Rectangle ControlBounds)
        {
            var frameGeometry = new TracerFrameGeometry();
            frameGeometry.ApexHeightFeet = ShotData.ApexHeightFeet;

            List<Vector3D> totalPoints = ShotData.TotalTrajectoryPoints;
            List<Vector3D> carryPoints = ShotData.CarryTrajectoryPoints;
            List<Vector3D> rolloutPoints = ShotData.RolloutTrajectoryPoints;
            int apexPointIndex = FindApexPointIndex(carryPoints);

            double flightPhaseFraction = Math.Min(AnimationProgress / AnimationFlightPhaseShare, 1.0);
            int lastCarryIndexToDraw = (int)Math.Round((carryPoints.Count - 1) * flightPhaseFraction);

            for (int pointIndex = 0; pointIndex <= lastCarryIndexToDraw; pointIndex++)
            {
                Vector3D currentPoint = carryPoints[pointIndex];
                double trajectoryFraction = pointIndex / (double)Math.Max(carryPoints.Count - 1, 1);
                double curveOffset = currentPoint.X * (CurveMultiplier - 1.0) * trajectoryFraction;
                Vector3D displayPoint = new Vector3D(
                    currentPoint.X + curveOffset,
                    currentPoint.Y,
                    currentPoint.Z);

                Vector3D groundBelowPoint = new Vector3D(displayPoint.X, displayPoint.Y, 0.0);

                PointF arcScreenPoint = ProjectTrajectoryPointToScreen(displayPoint, ControlBounds);
                PointF shadowScreenPoint = ProjectTrajectoryPointToScreen(groundBelowPoint, ControlBounds);

                frameGeometry.FlightArcScreenPoints.Add(arcScreenPoint);
                frameGeometry.GroundShadowScreenPoints.Add(shadowScreenPoint);

                frameGeometry.CurrentBallScreenPosition = arcScreenPoint;
                frameGeometry.CurrentDownrangeYards = currentPoint.Y;

                if (apexPointIndex >= 0 && pointIndex == apexPointIndex)
                {
                    frameGeometry.HasPassedApex = true;
                    frameGeometry.ApexScreenPosition = arcScreenPoint;
                }

                // Reuse this iteration's own shadowScreenPoint (ground level, with the same
                // curveOffset-adjusted X as the arc/shadow lines) for the landing marker instead
                // of re-projecting carryPoints[carryPoints.Count - 1] separately below: that
                // separate projection used the raw trajectory point's actual Z (which usually
                // isn't exactly 0 at the last sample) and skipped the curveOffset exaggeration
                // entirely, so the landing ring ended up both above and to the side of where the
                // flight arc and ground shadow actually met the ground -- reusing the loop's own
                // ground-level point guarantees the ring lines up with the visible landing spot.
                if (pointIndex == carryPoints.Count - 1)
                {
                    frameGeometry.HasLanded = true;
                    frameGeometry.LandingScreenPosition = shadowScreenPoint;
                }
            }

            if (AnimationProgress > AnimationFlightPhaseShare && rolloutPoints.Count > 0)
            {
                double rollPhaseFraction = (AnimationProgress - AnimationFlightPhaseShare)
                    / Math.Max(1.0 - AnimationFlightPhaseShare, 0.0001);

                int lastRollIndexToDraw = (int)Math.Round(
                    (rolloutPoints.Count - 1) * Math.Min(rollPhaseFraction, 1.0));

                for (int pointIndex = 0; pointIndex <= lastRollIndexToDraw; pointIndex++)
                {
                    Vector3D currentPoint = rolloutPoints[pointIndex];

                    // Unlike the carry loop above, this does NOT ramp trajectoryFraction back up
                    // from 0: the carry loop's curveOffset reaches its full strength
                    // (CurveMultiplier - 1.0, with no fractional discount) at the very last carry
                    // point, i.e. the landing spot, which is also the first point in
                    // RolloutTrajectoryPoints. Re-deriving a fresh 0-to-1 fraction over just the
                    // rollout points made curveOffset drop back to 0 at that shared landing point,
                    // then climb again -- a real, visible sideways jump ("kick") in the line right
                    // as rollout starts drawing, even though the underlying trajectory point
                    // hadn't moved. Holding the fraction at 1.0 for the whole rollout phase keeps
                    // curveOffset continuous with where the carry loop left off.
                    double curveOffset = currentPoint.X * (CurveMultiplier - 1.0);

                    Vector3D displayPoint = new Vector3D(
                        currentPoint.X + curveOffset,
                        currentPoint.Y,
                        currentPoint.Z);

                    PointF rollScreenPoint = ProjectTrajectoryPointToScreen(displayPoint, ControlBounds);

                    frameGeometry.RollScreenPoints.Add(rollScreenPoint);
                    frameGeometry.CurrentBallScreenPosition = rollScreenPoint;
                    frameGeometry.CurrentDownrangeYards = currentPoint.Y;
                }
            }

            frameGeometry.IsAnimationComplete = AnimationProgress >= 1.0;

            return frameGeometry;
        }

        /// <summary>
        /// Paints the control: the stretched background course image, the flight arc and ground
        /// shadow, the roll segment, the ball marker, the floating live-yardage callout, the apex
        /// callout, and the landing marker. If no shot is currently set, only the background
        /// image is drawn.
        /// </summary>
        /// <param name="PaintArguments">The paint event arguments supplied by WinForms.</param>
        protected override void OnPaint(PaintEventArgs PaintArguments)
        {
            base.OnPaint(PaintArguments);

            Graphics graphicsSurface = PaintArguments.Graphics;
            graphicsSurface.SmoothingMode = SmoothingMode.AntiAlias;
            graphicsSurface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle controlBounds = ClientRectangle;

            if (BackgroundCourseImage != null)
            {
                graphicsSurface.DrawImage(BackgroundCourseImage, controlBounds);
            }

            // Drawn unconditionally, before the "no shot yet" early-return below and with no
            // fade multiplier applied: these signs are part of the range itself, not part of the
            // current shot, so they need to be visible from the moment the control first paints
            // and must stay fully visible while a shot's tracer/shadow/roll/callouts fade out.
            //DrawYardageMarkers(graphicsSurface, controlBounds);

            if (_currentShotData == null)
            {
                return;
            }

            TracerFrameGeometry frameGeometry = BuildFrameGeometry(_currentShotData, _currentAnimationProgress, controlBounds);

            float fadeAlphaMultiplier = GetCurrentFadeAlphaMultiplier();

            using (Pen shadowPen = new Pen(ApplyFadeAlpha(GroundShadowColor, fadeAlphaMultiplier), 5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.GroundShadowScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(shadowPen, frameGeometry.GroundShadowScreenPoints.ToArray());
                }
            }

            using (Pen flightArcPen = new Pen(ApplyFadeAlpha(FlightArcColor, fadeAlphaMultiplier), 4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.FlightArcScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(flightArcPen, frameGeometry.FlightArcScreenPoints.ToArray());
                }
            }

            using (Pen rollPen = new Pen(ApplyFadeAlpha(RollSegmentColor, fadeAlphaMultiplier), 5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.RollScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(rollPen, frameGeometry.RollScreenPoints.ToArray());
                }
            }

            if (frameGeometry.HasLanded)
            {
                DrawLandingMarker(graphicsSurface, frameGeometry.LandingScreenPosition, fadeAlphaMultiplier);
            }

            DrawBallMarker(graphicsSurface, frameGeometry.CurrentBallScreenPosition, fadeAlphaMultiplier);

            if (frameGeometry.IsAnimationComplete)
            {
                //DrawTotalYardageCallout(graphicsSurface,frameGeometry,fadeAlphaMultiplier);
            }
            else
            {
                DrawLiveYardageCallout(graphicsSurface, frameGeometry, fadeAlphaMultiplier);
            }
        }

        //private void DrawTotalYardageCallout(Graphics GraphicsSurface, TracerFrameGeometry FrameGeometry, float AlphaMultiplier)
        //{
        //    string calloutText = $"TOTAL: {_currentShotData.TotalDistanceYards:0} yds";

        //    PointF calloutAnchorPosition = new PointF(
        //        FrameGeometry.CurrentBallScreenPosition.X + 14f,
        //        FrameGeometry.CurrentBallScreenPosition.Y - 30f);

        //    DrawCalloutBox(
        //        GraphicsSurface,
        //        calloutText,
        //        calloutAnchorPosition,
        //        LiveYardageBoxColor,
        //        AlphaMultiplier);
        //}
        private void DrawTotalYardageCallout(Graphics GraphicsSurface, TracerFrameGeometry FrameGeometry, float AlphaMultiplier)
        {
            double carryYards = _currentShotData.TotalCarryYards;
            double rolloutYards = _currentShotData.TotalRolloutYards;
            double totalYards = _currentShotData.TotalDistanceYards;

            string calloutText = $"Total: {totalYards:0} yds\nCarry: {carryYards:0} yds\nRollout: {rolloutYards:0} yds";

            // Position callout at the last rollout point
            PointF lastPoint = FrameGeometry.CurrentBallScreenPosition;
            double lateralOffset = 0; // Default to center

            if (_currentShotData.RolloutTrajectoryPoints != null && _currentShotData.RolloutTrajectoryPoints.Count > 0)
            {
                Vector3D lastRolloutPoint = _currentShotData.RolloutTrajectoryPoints[_currentShotData.RolloutTrajectoryPoints.Count - 1];
                lastPoint = ProjectTrajectoryPointToScreen(lastRolloutPoint, this.ClientRectangle);
                lateralOffset = lastRolloutPoint.X; // Get the lateral offset (positive = right, negative = left)
            }

            // Measure the text to know the box width and height
            SizeF measuredTextSize = GraphicsSurface.MeasureString(calloutText, CalloutFont);
            float boxWidth = measuredTextSize.Width + 16f; // HorizontalPadding * 2
            float boxHeight = measuredTextSize.Height + 8f; // VerticalPadding * 2

            // Position box based on shot direction
            // Left shot: upper RIGHT corner touches the last point
            // Right shot: upper LEFT corner touches the last point
            PointF calloutDisplayPosition;
            if (lateralOffset < 0)
            {
                // Shot went LEFT: upper right corner at last point
                calloutDisplayPosition = new PointF(lastPoint.X - boxWidth, lastPoint.Y - boxHeight);
            }
            else
            {
                // Shot went RIGHT: upper left corner at last point
                calloutDisplayPosition = new PointF(lastPoint.X, lastPoint.Y - boxHeight);
            }

            DrawCalloutBox(
                GraphicsSurface,
                calloutText,
                calloutDisplayPosition,
                LiveYardageBoxColor,
                AlphaMultiplier);
        }

        /// <summary>
        /// Draws each sign in <see cref="_yardageMarkerImages"/> along the ground at its
        /// configured downrange distance, using the same tee-to-horizon perspective math as
        /// <see cref="ProjectTrajectoryPointToScreen"/> so a marker's on-screen position and size
        /// shrink with distance exactly the way the flight arc does. Each sign is anchored at its
        /// bottom-center point (not its middle), so it reads as standing upright on the ground
        /// rather than floating around the ground line. Does nothing if
        /// <see cref="ShowYardageMarkers"/> is false.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        private void DrawYardageMarkers(Graphics GraphicsSurface, Rectangle ControlBounds)
        {
            if (!ShowYardageMarkers)
            {
                return;
            }

            InterpolationMode previousInterpolationMode = GraphicsSurface.InterpolationMode;
            GraphicsSurface.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Draw markers in reverse order so far markers (drawn first) appear behind near markers (drawn last)
            //for (int i = _yardageMarkerImages.Count - 1; i >= 0; i--)
            foreach (var marker in _yardageMarkerImages)
            {
                //var marker = _yardageMarkerImages[i];
                Vector3D groundPointAtMarker = new Vector3D(0.0, marker.DownrangeYards, 0.0);
                PointF groundScreenPosition = ProjectTrajectoryPointToScreen(groundPointAtMarker, ControlBounds);

                // Mirrors the normalizedDepth/perspectiveShrinkFactor calculation inside
                // ProjectTrajectoryPointToScreen so a marker's size shrinks with distance the same
                // way the flight arc's lateral/height offsets do, but floored at
                // MinimumYardageMarkerShrinkFactor so a distant sign (e.g. the 250-yard marker)
                // stays legible instead of shrinking toward zero as it nears the horizon.
                double linearDepth = Math.Min(
                    marker.DownrangeYards / Math.Max(LongestExpectedShotYards, 1.0), 1.0);
                double normalizedDepth = Math.Pow(linearDepth, PerspectivePower - .01);
                double perspectiveShrinkFactor = normalizedDepth;  // Far markers get higher values
                                                                   // double markerShrinkFactor = Math.Max((1.0 - perspectiveShrinkFactor) * 1.2, 0.06);  // Scale with very low minimum
                double markerShrinkFactor = (1.0 - perspectiveShrinkFactor) * 1.2;  // Pure inverse with 30% scale up, no minimum
                //double markerShrinkFactor = Math.Max((1.0 - perspectiveShrinkFactor) * 1.4, 0.15);  // Proper scale with small minimum

                // Sized and cropped from ContentBounds (the sign's actual non-transparent
                // artwork), not the full source image: different marker files have different
                // amounts of surrounding transparent canvas padding, and scaling from the raw
                // image dimensions made whichever file had the least padding render larger than
                // the rest at the same configured width. ContentBounds gives every marker the
                // same treatment regardless of how its own source file happens to be padded.
                int markerWidth = (int)Math.Round(YardageMarkerWidthAtTeePixels * markerShrinkFactor);
                double markerAspectRatio =
                    (double)marker.ContentBounds.Height / marker.ContentBounds.Width;
                int markerHeight = (int)Math.Round(markerWidth * markerAspectRatio);

                Rectangle destinationRectangle = new Rectangle(
                    (int)Math.Round(groundScreenPosition.X - (markerWidth / 2.0)),
                    (int)Math.Round(groundScreenPosition.Y - markerHeight),
                    markerWidth,
                    markerHeight);

                GraphicsSurface.DrawImage(
                    marker.MarkerImage,
                    destinationRectangle,
                    marker.ContentBounds,
                    GraphicsUnit.Pixel);
            }

            GraphicsSurface.InterpolationMode = previousInterpolationMode;
        }

        /// <summary>
        /// Draws a small filled circle at the ball's current on-screen position for the frame.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="BallScreenPosition">The ball's current position in screen coordinates.</param>
        /// <param name="AlphaMultiplier">
        /// The current fade-out multiplier (1.0 = fully opaque, 0.0 = fully transparent), applied
        /// to both the fill and the outline so the ball marker fades out along with the rest of
        /// the tracer.
        /// </param>
        private void DrawBallMarker(Graphics GraphicsSurface, PointF BallScreenPosition, float AlphaMultiplier)
        {
            const float BallMarkerRadius = 5f;

            using (Brush ballFillBrush = new SolidBrush(ApplyFadeAlpha(Color.White, AlphaMultiplier)))
            {
                GraphicsSurface.FillEllipse(
                    ballFillBrush,
                    BallScreenPosition.X - BallMarkerRadius,
                    BallScreenPosition.Y - BallMarkerRadius,
                    BallMarkerRadius * 2f,
                    BallMarkerRadius * 2f);
            }

            using (Pen ballOutlinePen = new Pen(ApplyFadeAlpha(Color.Black, AlphaMultiplier), 1.5f))
            {
                GraphicsSurface.DrawEllipse(
                    ballOutlinePen,
                    BallScreenPosition.X - BallMarkerRadius,
                    BallScreenPosition.Y - BallMarkerRadius,
                    BallMarkerRadius * 2f,
                    BallMarkerRadius * 2f);
            }
        }

        /// <summary>
        /// Draws an unfilled ring at the point where the ball first touched the ground, using
        /// <see cref="LandingMarkerColor"/>. Once drawn, the ring remains in place for the rest of
        /// the animation to mark the landing spot.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="LandingScreenPosition">The landing point in screen coordinates.</param>
        /// <param name="AlphaMultiplier">
        /// The current fade-out multiplier (1.0 = fully opaque, 0.0 = fully transparent).
        /// </param>
        //private void DrawLandingMarker(Graphics GraphicsSurface, PointF LandingScreenPosition, float AlphaMultiplier)
        //{
        //    const float LandingRingRadius = 9f;

        //    using (Pen landingRingPen = new Pen(ApplyFadeAlpha(LandingMarkerColor, AlphaMultiplier), 2.5f))
        //    {
        //        GraphicsSurface.DrawEllipse(
        //            landingRingPen,
        //            LandingScreenPosition.X - LandingRingRadius,
        //            LandingScreenPosition.Y - LandingRingRadius,
        //            LandingRingRadius * 2f,
        //            LandingRingRadius * 2f);
        //    }
        //}
        private void DrawLandingMarker(Graphics GraphicsSurface, PointF LandingScreenPosition, float AlphaMultiplier)
        {
            const float LandingRingRadius = 9f;

            float ringWidth = LandingRingRadius * 2f;
            float ringHeight = ringWidth * (float)LandingMarkerDepthScale;

            float ringCenterY = LandingScreenPosition.Y;

            RectangleF landingRingBounds = new RectangleF(
                LandingScreenPosition.X - (ringWidth / 2f),
                ringCenterY - (ringHeight / 2f),
                ringWidth,
                ringHeight);

            using (Pen landingRingPen = new Pen(
                ApplyFadeAlpha(LandingMarkerColor, AlphaMultiplier),
                2.5f))
            {
                GraphicsSurface.DrawEllipse(
                    landingRingPen,
                    landingRingBounds);
            }
        }
        /// <summary>
        /// Draws a small rounded callout box, offset above and to the side of the ball, showing
        /// the live downrange distance traveled so far this frame. The box follows the ball for
        /// the entire animation, so the displayed yardage updates continuously as the shot flies
        /// and rolls out.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="FrameGeometry">The current frame's geometry and readout values.</param>
        /// <param name="AlphaMultiplier">
        /// The current fade-out multiplier (1.0 = fully opaque, 0.0 = fully transparent).
        /// </param>
        private void DrawLiveYardageCallout(Graphics GraphicsSurface, TracerFrameGeometry FrameGeometry, float AlphaMultiplier)
        {
            string calloutText = $"{FrameGeometry.CurrentDownrangeYards:0} yds";
            PointF calloutAnchorPosition = new PointF(
                FrameGeometry.CurrentBallScreenPosition.X + 14f,
                FrameGeometry.CurrentBallScreenPosition.Y - 30f);

            DrawCalloutBox(GraphicsSurface, calloutText, calloutAnchorPosition, LiveYardageBoxColor, AlphaMultiplier);
        }

        /// <summary>
        /// Draws a small rounded, semi-transparent box containing the given text, anchored at
        /// its top-left corner to the given screen position. Used by the live-yardage callout.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="CalloutText">The text to display inside the box.</param>
        /// <param name="TopLeftScreenPosition">The screen position of the box's top-left corner.</param>
        /// <param name="BackgroundColor">The fill color of the box.</param>
        /// <param name="AlphaMultiplier">
        /// The current fade-out multiplier (1.0 = fully opaque, 0.0 = fully transparent), applied
        /// to both the box fill and the text so the callout fades out along with the rest of the
        /// tracer.
        /// </param>
        private void DrawCalloutBox(
            Graphics GraphicsSurface,
            string CalloutText,
            PointF TopLeftScreenPosition,
            Color BackgroundColor,
            float AlphaMultiplier)
        {
            const float HorizontalPadding = 8f;
            const float VerticalPadding = 4f;
            const float CornerRadius = 6f;

            SizeF measuredTextSize = GraphicsSurface.MeasureString(CalloutText, CalloutFont);
            RectangleF boxBounds = new RectangleF(
                TopLeftScreenPosition.X,
                TopLeftScreenPosition.Y,
                measuredTextSize.Width + (HorizontalPadding * 2f),
                measuredTextSize.Height + (VerticalPadding * 2f));

            using (GraphicsPath roundedBoxPath = CreateRoundedRectanglePath(boxBounds, CornerRadius))
            using (Brush boxFillBrush = new SolidBrush(ApplyFadeAlpha(BackgroundColor, AlphaMultiplier)))
            using (Brush textFillBrush = new SolidBrush(ApplyFadeAlpha(Color.White, AlphaMultiplier)))
            {
                GraphicsSurface.FillPath(boxFillBrush, roundedBoxPath);
                GraphicsSurface.DrawString(
                    CalloutText,
                    CalloutFont,
                    textFillBrush,
                    boxBounds.X + HorizontalPadding,
                    boxBounds.Y + VerticalPadding);
            }
        }

        /// <summary>
        /// Builds a <see cref="GraphicsPath"/> describing a rectangle with uniformly rounded
        /// corners, used to render the callout boxes with soft edges.
        /// </summary>
        /// <param name="RectangleBounds">The outer bounds of the rounded rectangle.</param>
        /// <param name="CornerRadius">The radius, in pixels, applied to each corner.</param>
        /// <returns>A closed graphics path describing the rounded rectangle.</returns>
        private GraphicsPath CreateRoundedRectanglePath(RectangleF RectangleBounds, float CornerRadius)
        {
            float diameter = CornerRadius * 2f;
            GraphicsPath roundedPath = new GraphicsPath();

            roundedPath.AddArc(RectangleBounds.X, RectangleBounds.Y, diameter, diameter, 180f, 90f);
            roundedPath.AddArc(RectangleBounds.Right - diameter, RectangleBounds.Y, diameter, diameter, 270f, 90f);
            roundedPath.AddArc(RectangleBounds.Right - diameter, RectangleBounds.Bottom - diameter, diameter, diameter, 0f, 90f);
            roundedPath.AddArc(RectangleBounds.X, RectangleBounds.Bottom - diameter, diameter, diameter, 90f, 90f);
            roundedPath.CloseFigure();

            return roundedPath;
        }
    }
}