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

using OpenSwingGolfSimulator.Physics.Models;
using OpenSwingGolfSimulator.Shared.Models;
using OpenSwingGolfSimulator.UI.EventArgs;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;

namespace OpenSwingGolfSimulator.UI.Controls
{


    /// <summary>
    /// A WinForms user control that renders a top-down ("radar" style) shot dispersion chart, in
    /// the style of a launch monitor's bird's-eye shot-pattern view: a yardage grid, an animated
    /// ground-track line for each shot that draws in, holds, and fades out, a permanent red dot at
    /// each shot's landing position, and a dispersion ellipse that grows to encompass every
    /// recorded dot and periodically holds and fades. Like <see cref="GolfShotTracerControl"/>,
    /// this control consumes the same <see cref="GolfShotTrajectoryData"/> records used to drive
    /// the side-view tracer — it simply reads the X (lateral) and Y (downrange) components of each
    /// trajectory point and ignores height, since a dispersion chart is a flat, orthographic plot
    /// with no perspective involved.
    /// </summary>
    [ToolboxItem(true)]
    public partial class GolfShotDispersionControl : UserControl
    {
        /// <summary>
        /// The number of seconds a fading element (a shot's line/label, or the dispersion ellipse)
        /// takes to go from fully visible to fully transparent once its hold period ends. Shared
        /// by every fade in the control so line fades and ellipse fades feel consistent. Not
        /// exposed to the designer since it is a fine-grained animation-feel constant rather than
        /// a behavior the control's stated spec calls out; adjust it here directly if you want
        /// fades to feel snappier or more gradual.
        /// </summary>
        private const double FadeOutDurationSeconds = 1.0;

        /// <summary>
        /// The multiplier applied to the standard deviation of the recorded landing dots' lateral
        /// and downrange positions when sizing the dispersion ellipse. A value of 1.0 draws an
        /// ellipse at exactly one standard deviation; higher values draw a looser, more inclusive
        /// ellipse. Not exposed to the designer since it is a statistical tuning constant rather
        /// than a visual style choice; adjust it here directly if the ellipse should hug the shot
        /// pattern more tightly or loosely.
        /// </summary>
        private const double EllipseStandardDeviationMultiplier = 1.5;

        /// <summary>
        /// The smallest radius, in yards, that either axis of the dispersion ellipse can shrink
        /// to. Without a floor, a single recorded shot (zero variance) would collapse the ellipse
        /// to an invisible point; this keeps the very first shot's ellipse visible as a small
        /// circle around its one dot.
        /// </summary>
        private const double MinimumEllipseRadiusYards = 3.0;

        /// <summary>
        /// The background field image drawn behind the chart, stretched to fill the control's
        /// client area. Intended for a top-down fairway texture (for example, the perspective-
        /// unwarped background generated alongside this control) rather than a ground-level
        /// photo, since this control renders a flat, orthographic top-down plot.
        /// </summary>
        [Category("Appearance")]
        [Description("The top-down field image drawn behind the dispersion chart, stretched to fill the control.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image BackgroundFieldImage { get; set; }

        /// <summary>
        /// The screen-relative position, expressed as a fraction of the control's width and
        /// height (each in the range 0 to 1), of the origin point — lateral offset 0, downrange
        /// distance 0 — that every shot's path and the yardage grid are measured from. Defaults to
        /// horizontally centered and near the bottom of the control, matching a chart where the
        /// tee sits at the bottom and downrange distance increases upward.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Adjust this the same way you would <c>TeeOffPositionFraction</c> on
        /// <see cref="GolfShotTracerControl"/>: if the tee marker in your background image doesn't
        /// line up with where shot paths start on screen, nudge X (left/right) and Y (up/down)
        /// here until it does.
        /// </para>
        /// <example>
        /// If your background image's tee marker sits slightly left of center and closer to the
        /// very bottom edge than the default assumes:
        /// <code>
        /// dispersionControl.OriginPositionFraction = new PointF(0.47f, 0.97f);
        /// </code>
        /// </example>
        /// </remarks>
        [Category("Field Layout")]
        [Description("Where the origin (0 lateral, 0 downrange) sits on screen, as a fraction of the control's width and height. Match this to the tee position in your background image.")]
        [DefaultValue(typeof(PointF), "0.525f, 0.92f")]
        public PointF OriginPositionFraction { get; set; } = new PointF(0.525f, 0.92f);

        /// <summary>
        /// The uniform scale, in pixels per yard, applied to both the lateral and downrange axes.
        /// Because this is a flat, orthographic top-down plot (unlike the perspective-projected
        /// <see cref="GolfShotTracerControl"/>), a single scale is used for both axes so that
        /// distances read correctly in every direction and the dispersion ellipse is never
        /// stretched out of proportion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Increase this to zoom in (shots spread out further across the control, useful for
        /// short-game or wedge distances); decrease it to zoom out (fits longer shots, like full
        /// driver swings, without running off the edges). If you resize the control, re-check this
        /// value — it is a fixed pixels-per-yard number, not something the control recalculates for
        /// you, so a much smaller control paired with an unchanged scale will run distant shots off
        /// its edges, and a much larger control will leave the shot pattern looking small and
        /// centered in a lot of empty space.
        /// </para>
        /// <example>
        /// To fit shots up to roughly 250 yards comfortably inside a control that is 900 pixels
        /// tall, with the origin near the bottom:
        /// <code>
        /// dispersionControl.YardsToPixelsScale = 3.2; // 250 yards * 3.2 ≈ 800px, leaving margin
        /// </code>
        /// </example>
        /// </remarks>
        [Category("Field Layout")]
        [Description("Pixels per yard, applied uniformly to both the lateral and downrange axes. Increase to zoom in, decrease to fit longer shots.")]
        [DefaultValue(3.2)]
        public double YardsToPixelsScale { get; set; } = 3.2;

        /// <summary>
        /// The spacing, in yards, between yardage grid lines drawn in both directions: curved
        /// downrange distance rings every <see cref="GridSpacingYards"/> of true real-world
        /// distance from the origin, and straight vertical lines every
        /// <see cref="GridSpacingYards"/> of lateral offset to the left and right of center.
        /// </summary>
        [Category("Grid")]
        [Description("Spacing, in yards, between the curved distance rings and the straight vertical lateral-offset lines.")]
        [DefaultValue(50.0)]
        public double GridSpacingYards { get; set; } = 50.0;

        /// <summary>
        /// The color used to draw the yardage grid lines.
        /// </summary>
        [Category("Grid")]
        [Description("The color of the yardage grid lines.")]
        [DefaultValue(typeof(Color), "90, 255, 255, 255")]
        public Color GridLineColor { get; set; } = Color.FromArgb(90, 255, 255, 255);

        /// <summary>
        /// The color used to draw the yardage numbers at the grid line intersections.
        /// </summary>
        [Category("Grid")]
        [Description("The color of the yardage numbers drawn where the grid lines meet.")]
        [DefaultValue(typeof(Color), "White")]
        public Color GridLabelColor { get; set; } = Color.White;

        /// <summary>
        /// The font used to render both the grid's yardage numbers and the floating lateral-offset
        /// callout shown once a shot finishes.
        /// </summary>
        [Category("Grid")]
        [Description("The font used for the grid's yardage numbers and the lateral-offset callout.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Font LabelFont { get; set; } = new Font("Segoe UI", 9f, FontStyle.Bold);

        /// <summary>
        /// The total wall-clock duration, in seconds, that a shot's ground-track line takes to
        /// animate drawing in, from the tee to the final resting position. This is the control's
        /// "line drawing speed" — lower values draw the line faster.
        /// </summary>
        [Category("Animation")]
        [Description("Time, in seconds, for a shot's ground-track line to draw in from tee to final position. Lower values draw the line faster.")]
        [DefaultValue(2.0)]
        public double LineDrawDurationSeconds { get; set; } = 2.0;

        /// <summary>
        /// The number of seconds a shot's ground-track line (and its lateral-offset callout, which
        /// shares the same timing) remains fully visible after it finishes drawing, before it
        /// begins to fade out over <see cref="FadeOutDurationSeconds"/>.
        /// </summary>
        [Category("Animation")]
        [Description("Seconds a shot's line and lateral-offset callout stay fully visible after drawing in, before they fade out.")]
        [DefaultValue(4.0)]
        public double LineHoldDurationSeconds { get; set; } = 4.0;

        /// <summary>
        /// The number of seconds the dispersion ellipse remains fully visible after being
        /// recomputed (which happens each time a new landing dot is added), before it begins to
        /// fade out over <see cref="FadeOutDurationSeconds"/>.
        /// </summary>
        [Category("Animation")]
        [Description("Seconds the dispersion ellipse stays fully visible after being recomputed for a new shot, before it fades out.")]
        [DefaultValue(4.0)]
        public double EllipseHoldDurationSeconds { get; set; } = 4.0;

        /// <summary>
        /// The color used to draw each shot's animated ground-track line.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of each shot's animated ground-track line.")]
        [DefaultValue(typeof(Color), "Orange")]
        public Color TrackLineColor { get; set; } = Color.Orange;

        /// <summary>
        /// The color used to draw the permanent dot marking each shot's landing position.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the permanent dot marking each shot's landing position.")]
        [DefaultValue(typeof(Color), "Red")]
        public Color LandingDotColor { get; set; } = Color.Red;

        /// <summary>
        /// The color used to draw the dispersion ellipse's outline and translucent fill.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the dispersion ellipse's outline and translucent fill.")]
        [DefaultValue(typeof(Color), "Turquoise")]
        public Color DispersionEllipseColor { get; set; } = Color.Turquoise;

        /// <summary>
        /// The color used to draw the floating lateral-offset callout text shown once a shot's
        /// line finishes drawing.
        /// </summary>
        [Category("Appearance")]
        [Description("The color of the lateral-offset callout text shown once a shot finishes.")]
        [DefaultValue(typeof(Color), "White")]
        public Color LateralOffsetTextColor { get; set; } = Color.White;

        /// <summary>
        /// The shots currently animating on the chart — drawing in, holding, or fading out. Each
        /// entry is removed once its line and callout have fully faded; its landing dot, added to
        /// <see cref="_landingDots"/> partway through its life, remains behind permanently.
        /// </summary>
        private readonly List<ActiveShotTrace> _activeTraces = new List<ActiveShotTrace>();

        /// <summary>
        /// Every landing dot recorded so far, across every shot ever added to the control. Never
        /// cleared except by an explicit call to <see cref="ClearAllShots"/>.
        /// </summary>
        private readonly List<LandingDot> _landingDots = new List<LandingDot>();

        /// <summary>
        /// The number of seconds elapsed since the dispersion ellipse was last recomputed (which
        /// happens each time a new landing dot is added). Drives the ellipse's hold-then-fade
        /// opacity independently of any single shot's own trace timing.
        /// </summary>
        private double _ellipseElapsedSeconds;

        /// <summary>
        /// The on-screen radius, in pixels, at which each landing dot is drawn. Shared between
        /// <see cref="DrawLandingDots"/> and the right-click hit-testing in
        /// <see cref="HitTestLandingDot(Point)"/> so a dot's clickable area always matches its
        /// visible size.
        /// </summary>
        private const float LandingDotRadiusPixels = 5f;

        /// <summary>
        /// Extra pixel radius added on top of <see cref="LandingDotRadiusPixels"/> when hit-
        /// testing a right-click against the landing dots, so a dot remains easy to target even
        /// when the click lands just outside its drawn circle.
        /// </summary>
        private const float LandingDotHitTestPaddingPixels = 4f;

        /// <summary>
        /// The index within <see cref="_landingDots"/> of the dot most recently targeted by a
        /// right-click, captured by <see cref="GolfShotDispersionControl_MouseDown"/> and
        /// consumed by <see cref="DotContextMenuStrip_Opening"/> and
        /// <see cref="DeleteDotMenuItem_Click"/>. Null when the most recent right-click did not
        /// land on any dot, which suppresses the context menu entirely.
        /// </summary>
        private int? _rightClickedLandingDotIndex;

        private readonly PointF[] _fairwayLeftBoundary =
{
    new PointF(0.02f, 0.95f),
    new PointF(0.03f, 0.80f),
    new PointF(0.06f, 0.65f),
    new PointF(0.10f, 0.50f),
    new PointF(0.14f, 0.35f),
    new PointF(0.18f, 0.20f),
    new PointF(0.22f, 0.05f)
};

        private readonly PointF[] _fairwayRightBoundary =
        {
    new PointF(0.96f, 0.95f),
    new PointF(0.94f, 0.80f),
    new PointF(0.91f, 0.65f),
    new PointF(0.88f, 0.50f),
    new PointF(0.84f, 0.35f),
    new PointF(0.78f, 0.20f),
    new PointF(0.70f, 0.05f)
};

        /// <summary>
        /// Initializes a new instance of the dispersion control, wiring up its designer-generated
        /// components.
        /// </summary>
        public GolfShotDispersionControl()
        {
            InitializeComponent();
        }
        private float GetBoundaryX(PointF[] BoundaryPoints, float NormalizedY)
        {
            if (BoundaryPoints == null || BoundaryPoints.Length == 0)
            {
                return 0f;
            }

            if (NormalizedY <= BoundaryPoints[0].Y)
            {
                return BoundaryPoints[0].X;
            }

            for (int pointIndex = 1;
                 pointIndex < BoundaryPoints.Length;
                 pointIndex++)
            {
                PointF previousPoint = BoundaryPoints[pointIndex - 1];
                PointF currentPoint = BoundaryPoints[pointIndex];

                if (NormalizedY <= currentPoint.Y)
                {
                    float range = currentPoint.Y - previousPoint.Y;

                    if (range <= 0f)
                    {
                        return currentPoint.X;
                    }

                    float fraction =
                        (NormalizedY - previousPoint.Y) / range;

                    return previousPoint.X +
                           ((currentPoint.X - previousPoint.X) * fraction);
                }
            }

            return BoundaryPoints[BoundaryPoints.Length - 1].X;
        }

        /// <summary>
        /// Applies the DPI-aware maximum size (see <see cref="ApplyMaximumSizeFromInches"/>) the
        /// moment the control's window handle becomes available, since an accurate DPI reading
        /// requires a real handle to measure against.
        /// </summary>
        /// <param name="EventArguments">Unused.</param>
        protected override void OnHandleCreated(System.EventArgs EventArguments)
        {
            base.OnHandleCreated(EventArguments);
            //ApplyMaximumSizeFromInches();
        }

        /// <summary>
        /// Adds a new shot to the dispersion chart. Its ground-track path is built directly from
        /// the shot's carry and rollout trajectory points (reading only the lateral and downrange
        /// components of each; height is ignored, since this is a flat top-down plot). The path
        /// animates drawing in, then a permanent red dot is dropped at the shot's final resting
        /// position and the dispersion ellipse is recomputed to include it, then the line and its
        /// lateral-offset callout hold briefly before fading out. Multiple shots can be mid-
        /// animation at once — each is tracked independently.
        /// </summary>
        /// <param name="ShotData">
        /// The same trajectory record consumed by <see cref="GolfShotTracerControl.StartShot(GolfShotTrajectoryData)"/>.
        /// <see cref="GolfShotTrajectoryData.CarryTrajectoryPoints"/> must contain at least two
        /// points, and <see cref="GolfShotTrajectoryData.RolloutTrajectoryPoints"/> must not be
        /// null (pass an empty list for a shot with no rollout).
        /// </param>
        public void AddShot(GolfShotTrajectoryData ShotData)
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

            List<PointF> combinedPathPointsYards = new List<PointF>();

            foreach (Vector3D carryPoint in ShotData.CarryTrajectoryPoints)
            {
                combinedPathPointsYards.Add(new PointF((float)carryPoint.X, (float)carryPoint.Y));
            }

            foreach (Vector3D rolloutPoint in ShotData.RolloutTrajectoryPoints)
            {
                combinedPathPointsYards.Add(new PointF((float)rolloutPoint.X, (float)rolloutPoint.Y));
            }

            PointF finalRestingPointYards = combinedPathPointsYards[combinedPathPointsYards.Count - 1];

            ActiveShotTrace newTrace = new ActiveShotTrace
            {
                PathPointsYards = combinedPathPointsYards,
                FinalLateralOffsetYards = finalRestingPointYards.X,
                ElapsedSeconds = 0.0,
                HasAddedLandingDot = false
            };

            _activeTraces.Add(newTrace);
            AnimationRefreshTimer.Start();
            Invalidate();
        }

        /// <summary>
        /// An <see cref="EventHandler{ShotRecordedEventArgs}"/>-compatible method that adds the
        /// shot carried by <paramref name="ShotEventArgs"/> to the dispersion chart. Exists so
        /// this control can be wired directly to a parent-level shot-recorded event without a
        /// wrapping lambda — for example:
        /// <c>someShotSource.OnShotRecorded += dispersionControl.OnShotRecorded;</c>. Internally
        /// this simply forwards to <see cref="AddShot(GolfShotTrajectoryData)"/>.
        /// </summary>
        /// <param name="EventSender">The source of the event (unused).</param>
        /// <param name="ShotEventArgs">The event args carrying the newly recorded shot.</param>
        public void OnShotRecorded(object EventSender, ShotRecordedEventArgs ShotEventArgs)
        {
            if (ShotEventArgs == null)
            {
                throw new ArgumentNullException(nameof(ShotEventArgs));
            }

            AddShot(ShotEventArgs.ShotRecord);
        }

        /// <summary>
        /// Immediately clears every animating shot line, every permanent landing dot, and the
        /// dispersion ellipse, resetting the chart to empty.
        /// </summary>
        public void ClearAllShots()
        {
            _activeTraces.Clear();
            _landingDots.Clear();
            _ellipseElapsedSeconds = 0.0;
            AnimationRefreshTimer.Stop();
            Invalidate();
        }

        /// <summary>
        /// Removes every permanent landing dot from the chart — the "dead" shots that have
        /// already come to rest — without touching any shot line still actively animating.
        /// Resets the dispersion ellipse along with the dots it was computed from, since an
        /// ellipse with no dots behind it has nothing left to describe. Unlike
        /// <see cref="ClearAllShots"/>, this leaves any in-flight ground-track lines and the
        /// animation timer running undisturbed. Intended to be wired up to an external "clear
        /// dots" command (for example a button on the parent form).
        /// </summary>
        public void ClearDeadDots()
        {
            _landingDots.Clear();
            _ellipseElapsedSeconds = 0.0;
            _rightClickedLandingDotIndex = null;
            Invalidate();
        }

        /// <summary>
        /// Handles a mouse-down anywhere on the control by hit-testing the click position against
        /// every recorded landing dot when the right mouse button is used, so the dot (if any)
        /// under the cursor is already known by the time <see cref="DotContextMenuStrip"/>
        /// decides whether to open. Left-button and other clicks are ignored.
        /// </summary>
        /// <param name="EventSender">The control that raised the event (unused).</param>
        /// <param name="MouseEventArguments">
        /// The mouse event arguments, including the click location and button.
        /// </param>
        private void GolfShotDispersionControl_MouseDown(object EventSender, MouseEventArgs MouseEventArguments)
        {
            if (MouseEventArguments.Button == MouseButtons.Right)
            {
                _rightClickedLandingDotIndex = HitTestLandingDot(MouseEventArguments.Location);
            }
        }

        /// <summary>
        /// Finds the landing dot, if any, whose on-screen circle contains the given client-area
        /// point (widened by <see cref="LandingDotHitTestPaddingPixels"/> for a more forgiving
        /// click target). When multiple dots overlap at the click location, the closest one is
        /// returned.
        /// </summary>
        /// <param name="ClientLocation">
        /// The mouse position, in control-relative client coordinates.
        /// </param>
        /// <returns>
        /// The index of the hit dot within <see cref="_landingDots"/>, or null if no dot was hit.
        /// </returns>
        private int? HitTestLandingDot(Point ClientLocation)
        {
            Rectangle controlBounds = ClientRectangle;
            float hitTestRadiusPixels = LandingDotRadiusPixels + LandingDotHitTestPaddingPixels;
            float hitTestRadiusSquared = hitTestRadiusPixels * hitTestRadiusPixels;

            int? closestDotIndex = null;
            float closestDistanceSquared = float.MaxValue;

            for (int dotIndex = 0; dotIndex < _landingDots.Count; dotIndex++)
            {
                PointF dotScreenPosition = ProjectYardsPointToScreen(
                    _landingDots[dotIndex].PositionYards, controlBounds);

                float offsetX = ClientLocation.X - dotScreenPosition.X;
                float offsetY = ClientLocation.Y - dotScreenPosition.Y;
                float distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);

                if (distanceSquared <= hitTestRadiusSquared && distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestDotIndex = dotIndex;
                }
            }

            return closestDotIndex;
        }

        /// <summary>
        /// Handles <see cref="DotContextMenuStrip"/>'s Opening event by cancelling the menu
        /// entirely when the right-click that triggered it (captured by
        /// <see cref="GolfShotDispersionControl_MouseDown"/>) did not land on any landing dot, so
        /// the menu never appears over empty field or background image.
        /// </summary>
        /// <param name="EventSender">The context menu that raised the event (unused).</param>
        /// <param name="CancelEventArguments">
        /// The event arguments used to cancel opening the menu.
        /// </param>
        private void DotContextMenuStrip_Opening(
            object EventSender, System.ComponentModel.CancelEventArgs CancelEventArguments)
        {
            if (!_rightClickedLandingDotIndex.HasValue)
            {
                CancelEventArguments.Cancel = true;
            }
        }

        /// <summary>
        /// Handles a click on the context menu's "Delete Dot" item by permanently removing the
        /// landing dot that was right-clicked, then recomputing the dispersion ellipse (or
        /// clearing it, if that was the last dot) so it always reflects only the dots still on
        /// the chart.
        /// </summary>
        /// <param name="EventSender">The menu item that raised the event (unused).</param>
        /// <param name="ClickEventArguments">The event arguments for the click (unused).</param>
        private void DeleteDotMenuItem_Click(object EventSender, System.EventArgs ClickEventArguments)
        {
            if (_rightClickedLandingDotIndex.HasValue
                && _rightClickedLandingDotIndex.Value < _landingDots.Count)
            {
                _landingDots.RemoveAt(_rightClickedLandingDotIndex.Value);
                _ellipseElapsedSeconds = 0.0;
                Invalidate();
            }

            _rightClickedLandingDotIndex = null;
        }

        /// <summary>
        /// Handles each tick of the animation timer by advancing every active shot trace's
        /// elapsed time, promoting any trace that has just finished drawing into a permanent
        /// landing dot (and recomputing the dispersion ellipse for it), removing any trace that
        /// has fully faded out, and advancing the dispersion ellipse's own elapsed time. Stops the
        /// timer once nothing remains to animate, to avoid repainting an idle control at 60 frames
        /// per second.
        /// </summary>
        /// <param name="SenderTimer">The animation timer that raised the event.</param>
        /// <param name="TickEventArgs">The event arguments for the timer tick (unused).</param>
        private void AnimationRefreshTimer_Tick(object SenderTimer, System.EventArgs TickEventArgs)
        {
            double secondsPerTick = AnimationRefreshTimer.Interval / 1000.0;

            for (int traceIndex = _activeTraces.Count - 1; traceIndex >= 0; traceIndex--)
            {
                ActiveShotTrace currentTrace = _activeTraces[traceIndex];
                currentTrace.ElapsedSeconds += secondsPerTick;

                bool hasFinishedDrawing = currentTrace.ElapsedSeconds >= LineDrawDurationSeconds;

                if (hasFinishedDrawing && !currentTrace.HasAddedLandingDot)
                {
                    currentTrace.HasAddedLandingDot = true;
                    _landingDots.Add(new LandingDot
                    {
                        PositionYards = currentTrace.PathPointsYards[currentTrace.PathPointsYards.Count - 1]
                    });
                    _ellipseElapsedSeconds = 0.0;
                }

                if (ComputeTraceOpacity(currentTrace) <= 0.0)
                {
                    _activeTraces.RemoveAt(traceIndex);
                }
            }

            if (_landingDots.Count > 0)
            {
                _ellipseElapsedSeconds += secondsPerTick;
            }

            bool anythingStillAnimating = _activeTraces.Count > 0
                || (_landingDots.Count > 0 && ComputeEllipseOpacity() > 0.0);

            if (!anythingStillAnimating)
            {
                AnimationRefreshTimer.Stop();
            }

            Invalidate();
        }

        /// <summary>
        /// Computes a shot trace's current opacity (0.0 to 1.0) based on its elapsed time: fully
        /// opaque while its line is still drawing in and throughout its hold period, then linearly
        /// fading to zero over <see cref="FadeOutDurationSeconds"/>. Shared by the line itself and
        /// its lateral-offset callout, so both fade in lockstep as the trace's spec requires.
        /// </summary>
        /// <param name="Trace">The shot trace to compute opacity for.</param>
        /// <returns>The trace's current opacity, from 0.0 (invisible) to 1.0 (fully visible).</returns>
        private double ComputeTraceOpacity(ActiveShotTrace Trace)
        {
            double postDrawElapsedSeconds = Trace.ElapsedSeconds - LineDrawDurationSeconds;

            if (postDrawElapsedSeconds <= 0.0)
            {
                return 1.0;
            }

            if (postDrawElapsedSeconds <= LineHoldDurationSeconds)
            {
                return 1.0;
            }

            double fadeElapsedSeconds = postDrawElapsedSeconds - LineHoldDurationSeconds;
            double fadeFraction = fadeElapsedSeconds / FadeOutDurationSeconds;

            return Math.Max(0.0, 1.0 - fadeFraction);
        }

        /// <summary>
        /// Computes the dispersion ellipse's current opacity (0.0 to 1.0) based on the time
        /// elapsed since it was last recomputed: fully opaque throughout
        /// <see cref="EllipseHoldDurationSeconds"/>, then linearly fading to zero over
        /// <see cref="FadeOutDurationSeconds"/>.
        /// </summary>
        /// <returns>The ellipse's current opacity, from 0.0 (invisible) to 1.0 (fully visible).</returns>
        private double ComputeEllipseOpacity()
        {
            if (_ellipseElapsedSeconds <= EllipseHoldDurationSeconds)
            {
                return 1.0;
            }

            double fadeElapsedSeconds = _ellipseElapsedSeconds - EllipseHoldDurationSeconds;
            double fadeFraction = fadeElapsedSeconds / FadeOutDurationSeconds;

            return Math.Max(0.0, 1.0 - fadeFraction);
        }

        /// <summary>
        /// Projects a point defined in flat field space (lateral offset and downrange distance,
        /// both in yards) onto a 2D screen-space point using a simple uniform orthographic scale —
        /// no perspective is involved, since this is a top-down plot. Downrange distance increases
        /// upward on screen (away from the origin), matching how a bird's-eye shot-dispersion
        /// chart is conventionally read.
        /// </summary>
        /// <param name="FieldPointYards">
        /// The point to project, in yards, where X is lateral offset (positive = right) and Y is
        /// downrange distance.
        /// </param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        /// <returns>The corresponding point in control-relative screen coordinates.</returns>
        //private PointF ProjectYardsPointToScreen(PointF FieldPointYards, Rectangle ControlBounds)
        //{
        //    float originScreenX = ControlBounds.Width * OriginPositionFraction.X;
        //    float originScreenY = ControlBounds.Height * OriginPositionFraction.Y;

        //    float screenX = originScreenX + (FieldPointYards.X * (float)YardsToPixelsScale);
        //    float screenY = originScreenY - (FieldPointYards.Y * (float)YardsToPixelsScale);

        //    return new PointF(screenX, screenY);
        //}
        private PointF ProjectYardsPointToScreen(
            PointF FieldPointYards,
            Rectangle ControlBounds)
        {
            float originScreenX =
                ControlBounds.Width * OriginPositionFraction.X;

            float originScreenY =
                ControlBounds.Height * OriginPositionFraction.Y;

            double yardsToPixelsScale =
                CalculateYardsToPixelsScale(ControlBounds);

            float screenX =
                originScreenX +
                (FieldPointYards.X * (float)yardsToPixelsScale);

            float screenY =
                originScreenY -
                (FieldPointYards.Y * (float)yardsToPixelsScale);

            return new PointF(screenX, screenY);
        }
        /// <summary>
        /// Computes the center and radii of the dispersion ellipse from every currently recorded
        /// landing dot: the center is the mean lateral and downrange position across all dots. Each
        /// axis's radius is that axis's sample standard deviation scaled by an effective multiplier
        /// that starts at <see cref="EllipseStandardDeviationMultiplier"/> but is increased, if
        /// necessary, to whatever value is required so that every currently recorded landing dot
        /// falls strictly inside the ellipse (with a small margin so the farthest dot is not sitting
        /// exactly on the boundary). A fixed multiplier alone only bounds a statistically typical
        /// shot — an outlier dot can still land outside a fixed-size ellipse — so this method
        /// guarantees full containment instead of just a statistical likelihood of it. Radii are
        /// floored at <see cref="MinimumEllipseRadiusYards"/> so a single dot still produces a
        /// visible circle. This is an axis-aligned standard-deviation ellipse, not a full
        /// principal-component (rotated) ellipse — simpler to compute and reason about, and visually
        /// sufficient for a shot-pattern overview.
        /// </summary>
        /// <returns>The computed ellipse geometry, in yards.</returns>
        //private DispersionEllipseGeometry ComputeDispersionEllipseGeometry()
        //{
        //    double lateralSum = 0.0;
        //    double downrangeSum = 0.0;

        //    foreach (LandingDot dot in _landingDots)
        //    {
        //        lateralSum += dot.PositionYards.X;
        //        downrangeSum += dot.PositionYards.Y;
        //    }

        //    double meanLateralYards = lateralSum / _landingDots.Count;
        //    double meanDownrangeYards = downrangeSum / _landingDots.Count;

        //    double lateralVarianceSum = 0.0;
        //    double downrangeVarianceSum = 0.0;

        //    foreach (LandingDot dot in _landingDots)
        //    {
        //        double lateralDifference = dot.PositionYards.X - meanLateralYards;
        //        double downrangeDifference = dot.PositionYards.Y - meanDownrangeYards;
        //        lateralVarianceSum += lateralDifference * lateralDifference;
        //        downrangeVarianceSum += downrangeDifference * downrangeDifference;
        //    }

        //    int sampleCountForVariance = Math.Max(_landingDots.Count - 1, 1);
        //    double lateralStandardDeviation = Math.Sqrt(lateralVarianceSum / sampleCountForVariance);
        //    double downrangeStandardDeviation = Math.Sqrt(downrangeVarianceSum / sampleCountForVariance);

        //    // A fixed multiplier on the standard deviation (the previous approach) only bounds a
        //    // *typical* shot statistically -- for a normal distribution, even a generous-looking
        //    // 1.5-standard-deviation ellipse leaves a real chance that an outlier dot lands outside
        //    // it, which is exactly what was observed. To guarantee every recorded dot actually
        //    // falls within the drawn ellipse, find the largest axis-normalized distance any single
        //    // dot has from the mean, and use that (or the usual statistical multiplier, whichever
        //    // is larger) to size both radii -- this keeps the ellipse's shape tied to the real
        //    // spread of the data while mathematically guaranteeing full containment.
        //    double requiredStandardDeviationMultiplier = EllipseStandardDeviationMultiplier;

        //    foreach (LandingDot dot in _landingDots)
        //    {
        //        double lateralDifference = dot.PositionYards.X - meanLateralYards;
        //        double downrangeDifference = dot.PositionYards.Y - meanDownrangeYards;

        //        double normalizedLateralDistance = lateralStandardDeviation > 0.0
        //            ? lateralDifference / lateralStandardDeviation
        //            : 0.0;
        //        double normalizedDownrangeDistance = downrangeStandardDeviation > 0.0
        //            ? downrangeDifference / downrangeStandardDeviation
        //            : 0.0;

        //        double normalizedDistanceFromCenter = Math.Sqrt(
        //            (normalizedLateralDistance * normalizedLateralDistance)
        //            + (normalizedDownrangeDistance * normalizedDownrangeDistance));

        //        requiredStandardDeviationMultiplier = Math.Max(
        //            requiredStandardDeviationMultiplier, normalizedDistanceFromCenter);
        //    }

        //    // A small margin so the farthest dot sits just inside the boundary rather than exactly
        //    // on it, where anti-aliasing could make it look like it's straddling the edge.
        //    const double ContainmentMarginMultiplier = 1.05;
        //    requiredStandardDeviationMultiplier *= ContainmentMarginMultiplier;

        //    double lateralRadiusYards = Math.Max(
        //        lateralStandardDeviation * requiredStandardDeviationMultiplier, MinimumEllipseRadiusYards);
        //    double downrangeRadiusYards = Math.Max(
        //        downrangeStandardDeviation * requiredStandardDeviationMultiplier, MinimumEllipseRadiusYards);

        //    return new DispersionEllipseGeometry
        //    {
        //        CenterYards = new PointF((float)meanLateralYards, (float)meanDownrangeYards),
        //        LateralRadiusYards = lateralRadiusYards,
        //        DownrangeRadiusYards = downrangeRadiusYards
        //    };
        //}
        private DispersionEllipseGeometry ComputeDispersionEllipseGeometry()
        {
            double lateralSum = 0.0;
            double downrangeSum = 0.0;

            foreach (LandingDot dot in _landingDots)
            {
                lateralSum += dot.PositionYards.X;
                downrangeSum += dot.PositionYards.Y;
            }

            double meanLateralYards =
                lateralSum / _landingDots.Count;

            double meanDownrangeYards =
                downrangeSum / _landingDots.Count;

            double maximumLateralDistanceYards = 0.0;
            double maximumDownrangeDistanceYards = 0.0;

            foreach (LandingDot dot in _landingDots)
            {
                double lateralDistance =
                    Math.Abs(dot.PositionYards.X - meanLateralYards);

                double downrangeDistance =
                    Math.Abs(dot.PositionYards.Y - meanDownrangeYards);

                maximumLateralDistanceYards =
                    Math.Max(maximumLateralDistanceYards, lateralDistance);

                maximumDownrangeDistanceYards =
                    Math.Max(maximumDownrangeDistanceYards, downrangeDistance);
            }

            // Start with the actual spread of the recorded landing points.
            double lateralRadiusYards =
                Math.Max(maximumLateralDistanceYards, MinimumEllipseRadiusYards);

            double downrangeRadiusYards =
                Math.Max(maximumDownrangeDistanceYards, MinimumEllipseRadiusYards);

            // Determine whether any point would fall outside the ellipse.
            double requiredContainmentScale = 1.0;

            foreach (LandingDot dot in _landingDots)
            {
                double lateralDifference =
                    dot.PositionYards.X - meanLateralYards;

                double downrangeDifference =
                    dot.PositionYards.Y - meanDownrangeYards;

                double normalizedLateralDistance =
                    lateralRadiusYards > 0.0
                        ? lateralDifference / lateralRadiusYards
                        : 0.0;

                double normalizedDownrangeDistance =
                    downrangeRadiusYards > 0.0
                        ? downrangeDifference / downrangeRadiusYards
                        : 0.0;

                double normalizedDistanceSquared =
                    (normalizedLateralDistance * normalizedLateralDistance) +
                    (normalizedDownrangeDistance * normalizedDownrangeDistance);

                requiredContainmentScale =
                    Math.Max(
                        requiredContainmentScale,
                        Math.Sqrt(normalizedDistanceSquared));
            }

            // Add a small visual margin around the outermost dots.
            const double ContainmentMargin = 1.05;

            double finalScale =
                requiredContainmentScale * ContainmentMargin;

            lateralRadiusYards *= finalScale;
            downrangeRadiusYards *= finalScale;

            return new DispersionEllipseGeometry
            {
                CenterYards = new PointF(
                    (float)meanLateralYards,
                    (float)meanDownrangeYards),

                LateralRadiusYards = lateralRadiusYards,
                DownrangeRadiusYards = downrangeRadiusYards
            };
        }
        /// <summary>
        /// Paints the control: the stretched background field image, the yardage grid, the
        /// dispersion ellipse, every active shot's ground-track line and lateral-offset callout,
        /// and finally the permanent landing dots on top of everything else.
        /// </summary>
        /// <param name="PaintArguments">The paint event arguments supplied by WinForms.</param>
        protected override void OnPaint(PaintEventArgs PaintArguments)
        {
            base.OnPaint(PaintArguments);

            Graphics graphicsSurface = PaintArguments.Graphics;
            graphicsSurface.SmoothingMode = SmoothingMode.AntiAlias;
            graphicsSurface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle controlBounds = ClientRectangle;

            if (BackgroundFieldImage != null)
            {
                graphicsSurface.DrawImage(BackgroundFieldImage, controlBounds);
            }

            DrawYardageGrid(graphicsSurface, controlBounds);

            if (_landingDots.Count > 0)
            {
                DrawDispersionEllipse(graphicsSurface, controlBounds);
            }

            foreach (ActiveShotTrace trace in _activeTraces)
            {
                DrawShotTrace(graphicsSurface, controlBounds, trace);
            }

            DrawLandingDots(graphicsSurface, controlBounds);
        }

        /// <summary>
        /// The number of straight-line segments used to approximate one curved distance ring.
        /// Higher values draw a smoother curve at a small extra drawing cost; 40 is smooth enough
        /// that the individual segments are not visible even on a wide ring.
        /// </summary>
        private const int DistanceRingSegmentCount = 40;

        /// <summary>
        /// Draws the yardage grid: a curved distance ring every <see cref="GridSpacingYards"/> of
        /// true real-world distance from the origin (the player), and a straight vertical line
        /// every <see cref="GridSpacingYards"/> of lateral offset to the left and right of center.
        /// Yardage numbers are drawn at the grid's intersections — downrange numbers along the
        /// vertical centerline (where each ring crosses lateral offset 0, which is also each
        /// ring's closest approach to the player), and lateral numbers along the origin row (where
        /// each vertical line crosses downrange distance 0), matching how a launch monitor's
        /// dispersion chart conventionally labels its axes.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        private void DrawYardageGrid(
     Graphics GraphicsSurface,
     Rectangle ControlBounds)
        {
            double yardsToPixelsScale =
                CalculateYardsToPixelsScale(ControlBounds);

            if (yardsToPixelsScale <= 0)
            {
                return;
            }

            double maximumDownrangeVisibleYards =
                (ControlBounds.Height * OriginPositionFraction.Y) /
                yardsToPixelsScale;

            double maximumLateralVisibleYards = Math.Max(
                ControlBounds.Width * OriginPositionFraction.X,
                ControlBounds.Width * (1.0 - OriginPositionFraction.X)) /
                yardsToPixelsScale;

            DrawCenterLine(
                GraphicsSurface,
                ControlBounds);

            using (Pen gridLinePen = new Pen(GridLineColor, 1f))
            {
                for (double distanceRingRadiusYards = GridSpacingYards;
                     distanceRingRadiusYards <= maximumDownrangeVisibleYards;
                     distanceRingRadiusYards += GridSpacingYards)
                {
                    PointF[] distanceRingScreenPoints = BuildDistanceRingScreenPoints(
                        distanceRingRadiusYards, maximumLateralVisibleYards, ControlBounds);

                    if (distanceRingScreenPoints.Length >= 2)
                    {
                        GraphicsSurface.DrawLines(gridLinePen, distanceRingScreenPoints);
                    }

                    PointF screenPointOnCenterline = ProjectYardsPointToScreen(
                        new PointF(0f, (float)distanceRingRadiusYards), ControlBounds);

                    DrawShadowedLabel(
                        GraphicsSurface,
                        $"{distanceRingRadiusYards:0}",
                        new PointF(screenPointOnCenterline.X + 4f, screenPointOnCenterline.Y - 16f));
                }

                //for (double lateralYards = GridSpacingYards;
                //     lateralYards <= maximumLateralVisibleYards;
                //     lateralYards += GridSpacingYards)
                //{
                //    double[] signedLateralOffsetsYards = { lateralYards, -lateralYards };

                //    foreach (double signedLateralOffsetYards in signedLateralOffsetsYards)
                //    {
                //        PointF screenPointOnOriginRow = ProjectYardsPointToScreen(
                //            new PointF((float)signedLateralOffsetYards, 0f), ControlBounds);

                //        GraphicsSurface.DrawLine(
                //            gridLinePen,
                //            screenPointOnOriginRow.X, ControlBounds.Top,
                //            screenPointOnOriginRow.X, ControlBounds.Bottom);

                //        string lateralDirectionSuffix = signedLateralOffsetYards > 0 ? "R" : "L";
                //        DrawShadowedLabel(
                //            GraphicsSurface,
                //            $"{Math.Abs(signedLateralOffsetYards):0} {lateralDirectionSuffix}",
                //            new PointF(screenPointOnOriginRow.X + 3f, screenPointOnOriginRow.Y + 4f));
                //    }
                //}
            }
        }

        private double CalculateYardsToPixelsScale(Rectangle ControlBounds)
        {
            const float topPadding = 20f;

            if (ControlBounds.Height <= 0)
            {
                return 0;
            }

            double originScreenY =
                ControlBounds.Height * OriginPositionFraction.Y;

            return (originScreenY - topPadding) / 300.0;
        }
        private void DrawCenterLine(
     Graphics GraphicsSurface,
     Rectangle ControlBounds)
        {
            const float topPadding = 15f;

            float centerX = ControlBounds.Width * OriginPositionFraction.X;

            float originY =
                ControlBounds.Height * OriginPositionFraction.Y;

            double pixelsPerYard =
                (originY - topPadding) / 300.0;

            float threeHundredYardY =
                originY - (float)(300.0 * pixelsPerYard);

            using (Pen centerLinePen = new Pen(GridLineColor, 1f))
            {
                GraphicsSurface.DrawLine(
                    centerLinePen,
                    centerX,
                    originY,
                    centerX,
                    threeHundredYardY);
            }
        }

        /// <summary>
        /// Builds the screen-space points approximating one curved distance ring: every point on
        /// the ring is exactly <paramref name="RadiusYards"/> true real-world yards from the
        /// origin (the player), not just <paramref name="RadiusYards"/> of downrange distance. A
        /// straight horizontal line at a fixed downrange distance would understate how far away a
        /// shot that also missed left or right actually finished (a shot 30 yards left and 140
        /// yards downrange is really <c>sqrt(30² + 140²) ≈ 143</c> yards away, not 140) — this
        /// draws the ring so its ends curve down toward the player as lateral offset increases,
        /// which is what keeps every point on it a true, constant real-world distance away.
        /// </summary>
        /// <param name="RadiusYards">
        /// The true real-world distance from the origin that every point on the ring represents.
        /// </param>
        /// <param name="MaximumLateralVisibleYards">
        /// The widest lateral offset, in yards, visible within the control at the current scale;
        /// the ring is not drawn any wider than this even if its own radius would allow it,
        /// matching a distance ring only being drawn as wide as the field itself.
        /// </param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        /// <returns>
        /// The ring's points in control-relative screen coordinates, ordered from its leftmost
        /// point to its rightmost point, ready to pass to <see cref="Graphics.DrawLines(Pen, PointF[])"/>.
        /// </returns>
        //private PointF[] BuildDistanceRingScreenPoints(
        //    double RadiusYards, double MaximumLateralVisibleYards, Rectangle ControlBounds)
        //{
        //    double lateralExtentYards = Math.Min(RadiusYards, MaximumLateralVisibleYards);
        //    PointF[] ringScreenPoints = new PointF[DistanceRingSegmentCount + 1];

        //    for (int segmentIndex = 0; segmentIndex <= DistanceRingSegmentCount; segmentIndex++)
        //    {
        //        double lateralFraction = ((segmentIndex / (double)DistanceRingSegmentCount) * 2.0) - 1.0;
        //        double lateralYards = lateralFraction * lateralExtentYards;

        //        // Pythagorean theorem, solved for the downrange leg: this is what keeps every
        //        // point on the curve exactly RadiusYards from the origin regardless of how far
        //        // left or right it sits.
        //        double downrangeYardsOnRing = Math.Sqrt(
        //            Math.Max((RadiusYards * RadiusYards) - (lateralYards * lateralYards), 0.0));

        //        ringScreenPoints[segmentIndex] = ProjectYardsPointToScreen(
        //            new PointF((float)lateralYards, (float)downrangeYardsOnRing), ControlBounds);
        //    }

        //    return ringScreenPoints;
        //}
        private PointF[] BuildDistanceRingScreenPoints(
    double RadiusYards,
    double MaximumLateralVisibleYards,
    Rectangle ControlBounds)
        {
            // Only use 62% of the available lateral width.
            double lateralExtentYards =
                Math.Min(RadiusYards, MaximumLateralVisibleYards * 0.62);

            PointF[] ringScreenPoints =
                new PointF[DistanceRingSegmentCount + 1];

            for (int segmentIndex = 0;
                 segmentIndex <= DistanceRingSegmentCount;
                 segmentIndex++)
            {
                double lateralFraction =
                    ((segmentIndex / (double)DistanceRingSegmentCount) * 2.0) - 1.0;

                double lateralYards =
                    lateralFraction * lateralExtentYards;

                double downrangeYardsOnRing =
                    Math.Sqrt(
                        Math.Max(
                            (RadiusYards * RadiusYards) -
                            (lateralYards * lateralYards),
                            0.0));

                ringScreenPoints[segmentIndex] =
                    ProjectYardsPointToScreen(
                        new PointF(
                            (float)lateralYards,
                            (float)downrangeYardsOnRing),
                        ControlBounds);
            }

            return ringScreenPoints;
        }
        /// <summary>
        /// Draws the dispersion ellipse computed from every recorded landing dot, applying the
        /// ellipse's current hold-then-fade opacity to both its translucent fill and its outline.
        /// Does nothing once the ellipse has fully faded.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        private void DrawDispersionEllipse(Graphics GraphicsSurface, Rectangle ControlBounds)
        {
            double ellipseOpacity = ComputeEllipseOpacity();

            if (ellipseOpacity <= 0.0)
            {
                return;
            }

            DispersionEllipseGeometry ellipseGeometry = ComputeDispersionEllipseGeometry();
            PointF ellipseCenterScreenPosition = ProjectYardsPointToScreen(
                ellipseGeometry.CenterYards, ControlBounds);

            float lateralRadiusPixels = (float)(ellipseGeometry.LateralRadiusYards * YardsToPixelsScale);
            float downrangeRadiusPixels = (float)(ellipseGeometry.DownrangeRadiusYards * YardsToPixelsScale);

            RectangleF ellipseScreenBounds = new RectangleF(
                ellipseCenterScreenPosition.X - lateralRadiusPixels,
                ellipseCenterScreenPosition.Y - downrangeRadiusPixels,
                lateralRadiusPixels * 2f,
                downrangeRadiusPixels * 2f);

            int outlineAlpha = (int)(DispersionEllipseColor.A * ellipseOpacity);
            int fillAlpha = (int)(outlineAlpha * 0.25);

            Color outlineColorWithOpacity = Color.FromArgb(outlineAlpha, DispersionEllipseColor);
            Color fillColorWithOpacity = Color.FromArgb(fillAlpha, DispersionEllipseColor);

            using (Brush ellipseFillBrush = new SolidBrush(fillColorWithOpacity))
            using (Pen ellipseOutlinePen = new Pen(outlineColorWithOpacity, 2.5f))
            {
                GraphicsSurface.FillEllipse(ellipseFillBrush, ellipseScreenBounds);
                GraphicsSurface.DrawEllipse(ellipseOutlinePen, ellipseScreenBounds);
            }
        }

        /// <summary>
        /// Draws a single shot trace: its ground-track polyline, grown up to the trace's current
        /// draw progress, and — once the line has fully drawn in — its floating lateral-offset
        /// callout. Both the line and the callout are drawn using the trace's current opacity, so
        /// they hold and fade out together as the control's spec requires.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        /// <param name="Trace">The shot trace to draw.</param>
        private void DrawShotTrace(Graphics GraphicsSurface, Rectangle ControlBounds, ActiveShotTrace Trace)
        {
            double traceOpacity = ComputeTraceOpacity(Trace);

            if (traceOpacity <= 0.0)
            {
                return;
            }

            double drawProgressFraction = Math.Min(Trace.ElapsedSeconds / LineDrawDurationSeconds, 1.0);
            int pointsToDrawCount = (int)Math.Round(
                (Trace.PathPointsYards.Count - 1) * drawProgressFraction) + 1;
            pointsToDrawCount = Math.Min(pointsToDrawCount, Trace.PathPointsYards.Count);

            List<PointF> screenPoints = new List<PointF>();

            for (int pointIndex = 0; pointIndex < pointsToDrawCount; pointIndex++)
            {
                screenPoints.Add(ProjectYardsPointToScreen(Trace.PathPointsYards[pointIndex], ControlBounds));
            }

            int lineAlpha = (int)(TrackLineColor.A * traceOpacity);
            Color lineColorWithOpacity = Color.FromArgb(lineAlpha, TrackLineColor);

            using (Pen trackLinePen = new Pen(lineColorWithOpacity, 3f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (screenPoints.Count >= 2)
                {
                    GraphicsSurface.DrawLines(trackLinePen, screenPoints.ToArray());
                }
            }

            if (drawProgressFraction >= 1.0)
            {
                DrawLateralOffsetCallout(GraphicsSurface, Trace, screenPoints[screenPoints.Count - 1], traceOpacity);
            }
        }

        /// <summary>
        /// Draws the floating lateral-offset callout for a completed shot — for example "13.2 R"
        /// or "5.0 L" — anchored just above the shot's final resting position on screen, faded to
        /// the given opacity so it disappears in lockstep with its line.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="Trace">The shot trace whose lateral offset should be displayed.</param>
        /// <param name="FinalScreenPosition">The shot's final resting position, in screen coordinates.</param>
        /// <param name="CalloutOpacity">The current opacity, from 0.0 to 1.0, to draw the callout at.</param>
        private void DrawLateralOffsetCallout(
            Graphics GraphicsSurface,
            ActiveShotTrace Trace,
            PointF FinalScreenPosition,
            double CalloutOpacity)
        {
            string lateralDirectionSuffix = Trace.FinalLateralOffsetYards >= 0 ? "R" : "L";
            string calloutText = $"{Math.Abs(Trace.FinalLateralOffsetYards):0.0} {lateralDirectionSuffix}";

            int textAlpha = (int)(LateralOffsetTextColor.A * CalloutOpacity);
            int shadowAlpha = (int)(180 * CalloutOpacity);

            Color textColorWithOpacity = Color.FromArgb(textAlpha, LateralOffsetTextColor);
            Color shadowColorWithOpacity = Color.FromArgb(shadowAlpha, Color.Black);

            PointF calloutPosition = new PointF(FinalScreenPosition.X + 10f, FinalScreenPosition.Y - 22f);
            PointF shadowPosition = new PointF(calloutPosition.X + 1f, calloutPosition.Y + 1f);

            using (Brush shadowBrush = new SolidBrush(shadowColorWithOpacity))
            using (Brush textBrush = new SolidBrush(textColorWithOpacity))
            {
                GraphicsSurface.DrawString(calloutText, LabelFont, shadowBrush, shadowPosition);
                GraphicsSurface.DrawString(calloutText, LabelFont, textBrush, calloutPosition);
            }
        }

        /// <summary>
        /// Draws every permanently recorded landing dot at full opacity — landing dots never fade,
        /// since they are what the dispersion ellipse is computed from.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="ControlBounds">The current client-area bounds of the control.</param>
        private void DrawLandingDots(Graphics GraphicsSurface, Rectangle ControlBounds)
        {
            using (Brush landingDotBrush = new SolidBrush(LandingDotColor))
            {
                foreach (LandingDot dot in _landingDots)
                {
                    PointF dotScreenPosition = ProjectYardsPointToScreen(dot.PositionYards, ControlBounds);

                    GraphicsSurface.FillEllipse(
                        landingDotBrush,
                        dotScreenPosition.X - LandingDotRadiusPixels,
                        dotScreenPosition.Y - LandingDotRadiusPixels,
                        LandingDotRadiusPixels * 2f,
                        LandingDotRadiusPixels * 2f);
                }
            }
        }

        /// <summary>
        /// Draws a small drop-shadowed text label — used for both the yardage grid numbers and
        /// available for any other short on-field text — so the label stays legible over a busy
        /// grass texture background regardless of the underlying image's brightness.
        /// </summary>
        /// <param name="GraphicsSurface">The graphics surface to draw onto.</param>
        /// <param name="LabelText">The text to draw.</param>
        /// <param name="TopLeftScreenPosition">The screen position of the label's top-left corner.</param>
        private void DrawShadowedLabel(Graphics GraphicsSurface, string LabelText, PointF TopLeftScreenPosition)
        {
            PointF shadowPosition = new PointF(TopLeftScreenPosition.X + 1f, TopLeftScreenPosition.Y + 1f);

            using (Brush shadowBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
            using (Brush labelBrush = new SolidBrush(GridLabelColor))
            {
                GraphicsSurface.DrawString(LabelText, LabelFont, shadowBrush, shadowPosition);
                GraphicsSurface.DrawString(LabelText, LabelFont, labelBrush, TopLeftScreenPosition);
            }
        }
        private bool IsGrassPixel(Color PixelColor)
        {
            return PixelColor.G > PixelColor.R * 1.08 &&
                   PixelColor.G > PixelColor.B * 1.10 &&
                   PixelColor.G > 45;
        }

        private bool IsScreenPositionGrass(
    PointF ScreenPosition,
    Rectangle ControlBounds)
        {
            if (BackgroundFieldImage == null)
            {
                return true;
            }

            if (ScreenPosition.X < ControlBounds.Left ||
                ScreenPosition.X >= ControlBounds.Right ||
                ScreenPosition.Y < ControlBounds.Top ||
                ScreenPosition.Y >= ControlBounds.Bottom)
            {
                return false;
            }

            int imageX = (int)(
                ScreenPosition.X *
                BackgroundFieldImage.Width /
                ControlBounds.Width);

            int imageY = (int)(
                ScreenPosition.Y *
                BackgroundFieldImage.Height /
                ControlBounds.Height);

            imageX = Math.Clamp(imageX, 0, BackgroundFieldImage.Width - 1);
            imageY = Math.Clamp(imageY, 0, BackgroundFieldImage.Height - 1);

            Color pixelColor =
                ((Bitmap)BackgroundFieldImage).GetPixel(imageX, imageY);

            return IsGrassPixel(pixelColor);
        }

        /// <summary>
        /// Determines whether the LabelFont property should be serialized at design time.
        /// </summary>
        private bool ShouldSerializeLabelFont() => !LabelFont.Equals(new Font("Segoe UI", 9f, FontStyle.Bold));

        /// <summary>
        /// Resets the LabelFont property to its default value.     
        /// </summary>
        private void ResetLabelFont() => LabelFont = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

}