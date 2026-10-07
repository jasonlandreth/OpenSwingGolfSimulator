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

using System;

namespace OpenSwingGolfSimulator.Physics.Models
{
    /// <summary>
    /// The fifteen shot-shape categories produced by crossing three start directions (Pull,
    /// straight start, Push) with five curve severities (Hook, Draw, no curve, Fade, Slice). This
    /// is the same 3x5 grid used by launch-monitor apps such as Awesome Golf. Names follow a
    /// right-handed golfer's perspective throughout: negative degree values are left, positive
    /// degree values are right, matching the sign convention already used by
    /// <see cref="GolfShotTrajectoryData.FaceToTargetDegrees"/> and
    /// <see cref="GolfShotTrajectoryData.FaceToPathDegrees"/>.
    /// </summary>
    public enum ShotShapeCategory
    {
        /// <summary>Starts left of target and curves further left, severely.</summary>
        PullHook,

        /// <summary>Starts left of target and curves gently further left.</summary>
        PullDraw,

        /// <summary>Starts left of target and flies straight from there (no meaningful curve).</summary>
        Pull,

        /// <summary>Starts left of target and curves gently back toward or past center.</summary>
        PullFade,

        /// <summary>Starts left of target and curves severely back toward or past center.</summary>
        PullSlice,

        /// <summary>Starts on target and curves left, severely.</summary>
        Hook,

        /// <summary>Starts on target and curves gently left.</summary>
        Draw,

        /// <summary>Starts on target and stays on target (no meaningful curve).</summary>
        Straight,

        /// <summary>Starts on target and curves gently right.</summary>
        Fade,

        /// <summary>Starts on target and curves right, severely.</summary>
        Slice,

        /// <summary>Starts right of target and curves severely back across the target, to the left.</summary>
        PushHook,

        /// <summary>Starts right of target and curves gently back toward or past center.</summary>
        PushDraw,

        /// <summary>Starts right of target and flies straight from there (no meaningful curve).</summary>
        Push,

        /// <summary>Starts right of target and curves gently further right.</summary>
        PushFade,

        /// <summary>Starts right of target and curves further right, severely.</summary>
        PushSlice
    }

    /// <summary>
    /// The configurable degree thresholds used to bucket a shot's Face-to-Target and Face-to-Path
    /// readings into one of the fifteen <see cref="ShotShapeCategory"/> values. Exposed as its own
    /// class (rather than hard-coded constants) so the cutoffs can be tuned per club or per
    /// player without touching the classification logic itself. Both thresholds are in degrees,
    /// matching the units of <see cref="GolfShotTrajectoryData.FaceToTargetDegrees"/> and
    /// <see cref="GolfShotTrajectoryData.FaceToPathDegrees"/>.
    /// </summary>
    public sealed class ShotShapeClassificationThresholds
    {
        /// <summary>
        /// The largest Face-to-Target magnitude, in degrees, still considered a "straight" start
        /// rather than a Pull or Push. For example, with the default of 2.0, a shot with a Face-
        /// to-Target reading anywhere from -2° to +2° is treated as starting on target.
        /// </summary>
        public double StartDirectionStraightThresholdDegrees { get; set; } = 2.0;

        /// <summary>
        /// The largest Face-to-Path magnitude, in degrees, still considered essentially no curve
        /// at all. For example, with the default of 1.5, a shot with a Face-to-Path reading under
        /// 1.5° in either direction is treated as flying straight from wherever it started.
        /// </summary>
        public double CurveStraightThresholdDegrees { get; set; } = 1.5;

        /// <summary>
        /// The Face-to-Path magnitude, in degrees, at and beyond which a curve is classified as
        /// the severe/unwanted variety (Hook or Slice) rather than the gentle/intentional variety
        /// (Draw or Fade). For example, with the default of 6.0, a Face-to-Path of -7° is a Hook;
        /// a Face-to-Path of -4° is a Draw.
        /// </summary>
        public double CurveSevereThresholdDegrees { get; set; } = 6.0;
    }

    /// <summary>
    /// The result of classifying a single shot: the resolved category, plus the underlying
    /// measurements that produced it. Included alongside the category so a caller can display the
    /// raw numbers (for example, "Push-Draw: 3.1° open to target, 4.2° closed to path, curved 9
    /// yards left") rather than only the label.
    /// </summary>
    public struct ShotShapeClassificationResult
    {
        /// <summary>The resolved shot-shape category.</summary>
        public ShotShapeCategory Category { get; set; }

        /// <summary>
        /// The Face-to-Target reading used to classify start direction, in degrees, copied
        /// directly from <see cref="GolfShotTrajectoryData.FaceToTargetDegrees"/>.
        /// </summary>
        public double StartDirectionDegrees { get; set; }

        /// <summary>
        /// The Face-to-Path reading used to classify curve severity, in degrees, copied directly
        /// from <see cref="GolfShotTrajectoryData.FaceToPathDegrees"/>.
        /// </summary>
        public double CurveAmountDegrees { get; set; }

        /// <summary>
        /// The shot's total curve, in yards, copied directly from
        /// <see cref="GolfShotTrajectoryData.CurveYards"/> for display purposes. Not used in the
        /// classification itself — see the type-level remarks on <see cref="ShotShapeClassifier"/>
        /// for why degrees, not yards, drive the curve bucketing.
        /// </summary>
        public double CurveYards { get; set; }
    }

    /// <summary>
    /// A static classifier that derives a shot's shape — one of the fifteen
    /// <see cref="ShotShapeCategory"/> values — directly from the real impact-condition metrics
    /// already captured on <see cref="GolfShotTrajectoryData"/>: <b>Face to Target</b> drives
    /// start direction (Pull / straight / Push), and <b>Face to Path</b> drives curve severity
    /// (Hook / Draw / straight / Fade / Slice). These are the same two numbers virtually every
    /// launch-monitor shot-shape classifier is built on, since they describe exactly what the
    /// clubface did at the moment it struck the ball — face angle relative to the target line
    /// determines where the ball starts, and face angle relative to the swing path determines how
    /// it curves from there.
    /// </summary>
    /// <remarks>
    /// Curve severity is bucketed from <see cref="GolfShotTrajectoryData.FaceToPathDegrees"/>
    /// rather than <see cref="GolfShotTrajectoryData.CurveYards"/> on purpose: degrees of Face-to-
    /// Path describe the impact condition itself and stay meaningful across every club in the
    /// bag, while yards of curve scale with ball speed and carry distance — the same Face-to-Path
    /// reading produces a much bigger yardage curve on a driver than on a wedge. A single yard-
    /// based threshold table would therefore misclassify short clubs as "straighter" than they
    /// really are. <see cref="GolfShotTrajectoryData.CurveYards"/> is still carried through into
    /// <see cref="ShotShapeClassificationResult.CurveYards"/> for display, just not for bucketing.
    /// </remarks>
    public static class ShotShapeClassifier
    {
        /// <summary>
        /// Classifies a shot's shape from its Face-to-Target and Face-to-Path readings, using the
        /// default thresholds in a new <see cref="ShotShapeClassificationThresholds"/>.
        /// </summary>
        /// <param name="ShotData">The shot to classify.</param>
        /// <returns>The classification result, including the resolved category and the underlying readings.</returns>
        public static ShotShapeClassificationResult Classify(GolfShotTrajectoryData ShotData)
        {
            return Classify(ShotData, new ShotShapeClassificationThresholds());
        }

        /// <summary>
        /// Classifies a shot's shape from its Face-to-Target and Face-to-Path readings, using the
        /// supplied threshold configuration.
        /// </summary>
        /// <param name="ShotData">The shot to classify.</param>
        /// <param name="Thresholds">
        /// The degree thresholds to classify with. Pass a customized instance to tune sensitivity
        /// per club or player.
        /// </param>
        /// <returns>The classification result, including the resolved category and the underlying readings.</returns>
        public static ShotShapeClassificationResult Classify(
            GolfShotTrajectoryData ShotData, ShotShapeClassificationThresholds Thresholds)
        {
            if (ShotData == null)
            {
                throw new ArgumentNullException(nameof(ShotData));
            }

            if (Thresholds == null)
            {
                throw new ArgumentNullException(nameof(Thresholds));
            }

            ShotShapeCategory category = DetermineCategory(
                ShotData.FaceToTargetDegrees, ShotData.FaceToPathDegrees, Thresholds);

            return new ShotShapeClassificationResult
            {
                Category = category,
                StartDirectionDegrees = ShotData.FaceToTargetDegrees,
                CurveAmountDegrees = ShotData.FaceToPathDegrees,
                CurveYards = ShotData.CurveYards
            };
        }

        /// <summary>
        /// Converts a <see cref="ShotShapeCategory"/> into a display-friendly, hyphenated label
        /// (for example, <see cref="ShotShapeCategory.PushDraw"/> becomes "Push-Draw"), suitable
        /// for showing directly in the UI.
        /// </summary>
        /// <param name="Category">The category to format.</param>
        /// <returns>A hyphenated display label for the category.</returns>
        public static string ToDisplayLabel(ShotShapeCategory Category)
        {
            switch (Category)
            {
                case ShotShapeCategory.PullHook: return "Pull-Hook";
                case ShotShapeCategory.PullDraw: return "Pull-Draw";
                case ShotShapeCategory.Pull: return "Pull";
                case ShotShapeCategory.PullFade: return "Pull-Fade";
                case ShotShapeCategory.PullSlice: return "Pull-Slice";
                case ShotShapeCategory.Hook: return "Hook";
                case ShotShapeCategory.Draw: return "Draw";
                case ShotShapeCategory.Straight: return "Straight";
                case ShotShapeCategory.Fade: return "Fade";
                case ShotShapeCategory.Slice: return "Slice";
                case ShotShapeCategory.PushHook: return "Push-Hook";
                case ShotShapeCategory.PushDraw: return "Push-Draw";
                case ShotShapeCategory.Push: return "Push";
                case ShotShapeCategory.PushFade: return "Push-Fade";
                case ShotShapeCategory.PushSlice: return "Push-Slice";
                default: throw new ArgumentOutOfRangeException(nameof(Category));
            }
        }

        /// <summary>
        /// Buckets a Face-to-Target reading and a Face-to-Path reading into one of the fifteen
        /// <see cref="ShotShapeCategory"/> values using the supplied thresholds.
        /// </summary>
        /// <param name="FaceToTargetDegrees">
        /// The clubface angle relative to the target line, in degrees, used to classify start
        /// direction.
        /// </param>
        /// <param name="FaceToPathDegrees">
        /// The clubface angle relative to the club path, in degrees, used to classify curve
        /// severity.
        /// </param>
        /// <param name="Thresholds">The degree thresholds to classify with.</param>
        /// <returns>The resolved shot-shape category.</returns>
        private static ShotShapeCategory DetermineCategory(
            double FaceToTargetDegrees, double FaceToPathDegrees, ShotShapeClassificationThresholds Thresholds)
        {
            bool startsLeft = FaceToTargetDegrees < -Thresholds.StartDirectionStraightThresholdDegrees;
            bool startsRight = FaceToTargetDegrees > Thresholds.StartDirectionStraightThresholdDegrees;

            bool curvesSeverelyLeft = FaceToPathDegrees < -Thresholds.CurveSevereThresholdDegrees;
            bool curvesGentlyLeft = !curvesSeverelyLeft
                && FaceToPathDegrees < -Thresholds.CurveStraightThresholdDegrees;
            bool curvesSeverelyRight = FaceToPathDegrees > Thresholds.CurveSevereThresholdDegrees;
            bool curvesGentlyRight = !curvesSeverelyRight
                && FaceToPathDegrees > Thresholds.CurveStraightThresholdDegrees;

            if (startsLeft)
            {
                if (curvesSeverelyLeft)
                {
                    return ShotShapeCategory.PullHook;
                }

                if (curvesGentlyLeft)
                {
                    return ShotShapeCategory.PullDraw;
                }

                if (curvesSeverelyRight)
                {
                    return ShotShapeCategory.PullSlice;
                }

                if (curvesGentlyRight)
                {
                    return ShotShapeCategory.PullFade;
                }

                return ShotShapeCategory.Pull;
            }

            if (startsRight)
            {
                if (curvesSeverelyLeft)
                {
                    return ShotShapeCategory.PushHook;
                }

                if (curvesGentlyLeft)
                {
                    return ShotShapeCategory.PushDraw;
                }

                if (curvesSeverelyRight)
                {
                    return ShotShapeCategory.PushSlice;
                }

                if (curvesGentlyRight)
                {
                    return ShotShapeCategory.PushFade;
                }

                return ShotShapeCategory.Push;
            }

            if (curvesSeverelyLeft)
            {
                return ShotShapeCategory.Hook;
            }

            if (curvesGentlyLeft)
            {
                return ShotShapeCategory.Draw;
            }

            if (curvesSeverelyRight)
            {
                return ShotShapeCategory.Slice;
            }

            if (curvesGentlyRight)
            {
                return ShotShapeCategory.Fade;
            }

            return ShotShapeCategory.Straight;
        }
    }
}
