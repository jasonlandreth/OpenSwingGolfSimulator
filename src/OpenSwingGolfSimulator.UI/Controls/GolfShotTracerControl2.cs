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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

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
    public partial class GolfShotTracerControl2 : UserControl
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
        [DefaultValue(typeof(PointF), "0.5F, 0.88F")]
        public PointF TeeOffPositionFraction { get; set; } = new PointF(0.5F, 0.88F);

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
        /// tracerControl.HorizonPositionFraction = new PointF(0.5F, 0.34Ff);
        /// </code>
        /// </example>
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Where the flight path converges to in the distance, as a fraction of the control's width and height. Match this to the tree line / horizon in your background image; it controls how tall and how curved the whole arc looks.")]
        [DefaultValue(typeof(PointF), "0.5F, 0.34F")]
        public PointF HorizonPositionFraction { get; set; } = new PointF(0.5F, 0.34F);

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
        /// Raised from an earlier default of 1.85 to 4.5: at 1.85, a typical short shot's apex
        /// (a few dozen feet) only produced a few dozen pixels of visible arc height, making every
        /// shot look flat and short regardless of its actual trajectory. If shots still look too
        /// flat or too tall after this change, this is the value to keep adjusting — it scales
        /// linearly, so doubling it doubles the visible arc height at every point along the
        /// flight.
        /// </remarks>
        [Category("2.5D Perspective")]
        [Description("Pixels per foot of height at the tee, before perspective shrinks it with distance. Uses feet to match TrajectoryPoint3D.Z.")]
        [DefaultValue(7.0)]
        public double HeightFeetToPixelsScale { get; set; } = 7.0;// 4.5;

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
        [DefaultValue(typeof(Font), "Segoe UI, 11f, style=Bold")]
        public Font CalloutFont { get; set; } = new Font("Segoe UI", 11f, FontStyle.Bold);

        [Category("2.5D Perspective")]
        [Description("Controls how downrange distance is visually distributed across the perspective image. Lower values push shots farther downrange; higher values compress them toward the horizon.")]
        [DefaultValue(0.20)]
        public double PerspectivePower { get; set; } = .30;// 0.20;
        [Category("Yardage Markers")]
        [Description("The distance in yards between each yardage marker sign.")]
        [DefaultValue(50.0)]
        public double YardageMarkerSpacingYards { get; set; } = 50.0;

        [Category("Yardage Markers")]
        [Description("The maximum distance in yards at which yardage marker signs are displayed.")]
        [DefaultValue(300.0)]
        public double YardageMarkerMaximumDistanceYards { get; set; } = 300.0;

        [Category("Yardage Markers")]
        [Description("The background color of the yardage marker signs.")]
        [DefaultValue(typeof(Color), "Black")]
        public Color YardageMarkerColor { get; set; } = Color.Black;

        [Category("Yardage Markers")]
        [Description("The color of the yardage marker text.")]
        [DefaultValue(typeof(Color), "White")]
        public Color YardageMarkerTextColor { get; set; } = Color.White;

        [Category("Yardage Markers")]
        [Description("The color of the yardage marker posts.")]
        [DefaultValue(typeof(Color), "White")]
        public Color YardageMarkerPostColor { get; set; } = Color.White;

        [Category("Yardage Markers")]
        [Description("The color of the yardage marker border.")]
        [DefaultValue(typeof(Color), "White")]
        public Color YardageMarkerBorderColor { get; set; } = Color.White;

        [Category("Yardage Markers")]
        [Description("The font used for the yardage marker text.")]
        [DefaultValue(typeof(Font), "Segoe UI, 9pt, style=Bold")]
        public Font YardageMarkerFont { get; set; } = new Font("Segoe UI", 9f, FontStyle.Bold);
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
        public GolfShotTracerControl2()
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
        private void AnimationRefreshTimer_Tick(object SenderTimer,System.EventArgs TickEventArgs)
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
                    ShotAnimationCompleted?.Invoke(this,System.EventArgs.Empty);

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
        //private PointF ProjectTrajectoryPointToScreen(TrajectoryPoint3D TrajectoryPoint, Rectangle ControlBounds)
        //{
        //    PointF teeScreenPosition = new PointF(
        //        ControlBounds.Width * TeeOffPositionFraction.X,
        //        ControlBounds.Height * TeeOffPositionFraction.Y);

        //    PointF horizonScreenPosition = new PointF(
        //        ControlBounds.Width * HorizonPositionFraction.X,
        //        ControlBounds.Height * HorizonPositionFraction.Y);

        //    double downrangeDistanceYards = TrajectoryPoint.Y;
        //    double lateralOffsetYards = TrajectoryPoint.X;
        //    double heightFeet = TrajectoryPoint.Z;

        //    double normalizedDepth = Math.Min(
        //        downrangeDistanceYards / Math.Max(LongestExpectedShotYards, 1.0), 1.0);

        //    float baseScreenPositionX = teeScreenPosition.X
        //        + ((horizonScreenPosition.X - teeScreenPosition.X) * (float)normalizedDepth);
        //    float baseScreenPositionY = teeScreenPosition.Y
        //        + ((horizonScreenPosition.Y - teeScreenPosition.Y) * (float)normalizedDepth);

        //    double perspectiveShrinkFactor = 1.0 - normalizedDepth;
        //    float lateralScreenOffset = (float)(lateralOffsetYards
        //        * LateralYardsToPixelsScale * perspectiveShrinkFactor);
        //    float heightScreenOffset = (float)(heightFeet
        //        * HeightFeetToPixelsScale * perspectiveShrinkFactor);

        //    return new PointF(
        //        baseScreenPositionX + lateralScreenOffset,
        //        baseScreenPositionY - heightScreenOffset);
        //}
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
        private TracerFrameGeometry BuildFrameGeometry(GolfShotTrajectoryData ShotData,double AnimationProgress, Rectangle ControlBounds)
        {
            TracerFrameGeometry frameGeometry = new TracerFrameGeometry();
            frameGeometry.ApexHeightFeet = ShotData.ApexHeightFeet;

            List<Vector3D> carryPoints = ShotData.CarryTrajectoryPoints;
            List<Vector3D> rolloutPoints = ShotData.RolloutTrajectoryPoints;
            int apexPointIndex = FindApexPointIndex(carryPoints);

            double flightPhaseFraction = Math.Min(AnimationProgress / AnimationFlightPhaseShare, 1.0);
            int lastCarryIndexToDraw = (int)Math.Round((carryPoints.Count - 1) * flightPhaseFraction);

            for (int pointIndex = 0; pointIndex <= lastCarryIndexToDraw; pointIndex++)
            {
                Vector3D currentPoint = carryPoints[pointIndex];
                Vector3D groundBelowPoint = new Vector3D(currentPoint.X, currentPoint.Y, 0.0);

                PointF arcScreenPoint = ProjectTrajectoryPointToScreen(currentPoint, ControlBounds);
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
            }

            if (lastCarryIndexToDraw >= carryPoints.Count - 1)
            {
                frameGeometry.HasLanded = true;
                frameGeometry.LandingScreenPosition = ProjectTrajectoryPointToScreen(
                    carryPoints[carryPoints.Count - 1], ControlBounds);
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
                    PointF rollScreenPoint = ProjectTrajectoryPointToScreen(currentPoint, ControlBounds);

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
        //protected override void OnPaint(PaintEventArgs PaintArguments)
        //{
        //    base.OnPaint(PaintArguments);

        //    Graphics graphicsSurface = PaintArguments.Graphics;
        //    graphicsSurface.SmoothingMode = SmoothingMode.AntiAlias;
        //    graphicsSurface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        //    Rectangle controlBounds = ClientRectangle;

        //    if (BackgroundCourseImage != null)
        //    {
        //        graphicsSurface.DrawImage(BackgroundCourseImage, controlBounds);
        //    }

        //    if (_currentShotData == null)
        //    {
        //        return;
        //    }

        //    TracerFrameGeometry frameGeometry = BuildFrameGeometry(
        //        _currentShotData, _currentAnimationProgress, controlBounds);

        //    float fadeAlphaMultiplier = GetCurrentFadeAlphaMultiplier();

        //    using (Pen shadowPen = new Pen(ApplyFadeAlpha(GroundShadowColor, fadeAlphaMultiplier), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        //    {
        //        if (frameGeometry.GroundShadowScreenPoints.Count >= 2)
        //        {
        //            graphicsSurface.DrawLines(shadowPen, frameGeometry.GroundShadowScreenPoints.ToArray());
        //        }
        //    }

        //    using (Pen flightArcPen = new Pen(ApplyFadeAlpha(FlightArcColor, fadeAlphaMultiplier), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        //    {
        //        if (frameGeometry.FlightArcScreenPoints.Count >= 2)
        //        {
        //            graphicsSurface.DrawLines(flightArcPen, frameGeometry.FlightArcScreenPoints.ToArray());
        //        }
        //    }

        //    using (Pen rollPen = new Pen(ApplyFadeAlpha(RollSegmentColor, fadeAlphaMultiplier), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        //    {
        //        if (frameGeometry.RollScreenPoints.Count >= 2)
        //        {
        //            graphicsSurface.DrawLines(rollPen, frameGeometry.RollScreenPoints.ToArray());
        //        }
        //    }

        //    if (frameGeometry.HasLanded)
        //    {
        //        DrawLandingMarker(graphicsSurface, frameGeometry.LandingScreenPosition, fadeAlphaMultiplier);
        //    }

        //    DrawBallMarker(graphicsSurface, frameGeometry.CurrentBallScreenPosition, fadeAlphaMultiplier);
        //    DrawLiveYardageCallout(graphicsSurface, frameGeometry, fadeAlphaMultiplier);
        //}
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

            if (_currentShotData == null)
            {
                return;
            }

            TracerFrameGeometry frameGeometry = BuildFrameGeometry(
                _currentShotData, _currentAnimationProgress, controlBounds);

            float fadeAlphaMultiplier = GetCurrentFadeAlphaMultiplier();

            using (Pen shadowPen = new Pen(
                ApplyFadeAlpha(GroundShadowColor, fadeAlphaMultiplier), 5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.GroundShadowScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(
                        shadowPen,
                        frameGeometry.GroundShadowScreenPoints.ToArray());
                }
            }

            using (Pen flightArcPen = new Pen(
                ApplyFadeAlpha(FlightArcColor, fadeAlphaMultiplier), 4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.FlightArcScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(
                        flightArcPen,
                        frameGeometry.FlightArcScreenPoints.ToArray());
                }
            }

            using (Pen rollPen = new Pen(
                ApplyFadeAlpha(RollSegmentColor, fadeAlphaMultiplier), 5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (frameGeometry.RollScreenPoints.Count >= 2)
                {
                    graphicsSurface.DrawLines(
                        rollPen,
                        frameGeometry.RollScreenPoints.ToArray());
                }
            }

            if (frameGeometry.HasLanded)
            {
                DrawLandingMarker(
                    graphicsSurface,
                    frameGeometry.LandingScreenPosition,
                    fadeAlphaMultiplier);
            }

            DrawBallMarker(
                graphicsSurface,
                frameGeometry.CurrentBallScreenPosition,
                fadeAlphaMultiplier);

            if (frameGeometry.IsAnimationComplete)
            {
                DrawTotalYardageCallout(
                    graphicsSurface,
                    frameGeometry,
                    fadeAlphaMultiplier);
            }
            else
            {
                DrawLiveYardageCallout(
                    graphicsSurface,
                    frameGeometry,
                    fadeAlphaMultiplier);
            }
        }

        private void DrawTotalYardageCallout(Graphics GraphicsSurface, TracerFrameGeometry FrameGeometry, float AlphaMultiplier)
        {
            string calloutText = $"TOTAL: {_currentShotData.TotalDistanceYards:0} yds";

            PointF calloutAnchorPosition = new PointF(
                FrameGeometry.CurrentBallScreenPosition.X + 14f,
                FrameGeometry.CurrentBallScreenPosition.Y - 30f);

            DrawCalloutBox(
                GraphicsSurface,
                calloutText,
                calloutAnchorPosition,
                LiveYardageBoxColor,
                AlphaMultiplier);
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
        private void DrawLandingMarker(Graphics GraphicsSurface, PointF LandingScreenPosition, float AlphaMultiplier)
        {
            const float LandingRingRadius = 9f;

            using (Pen landingRingPen = new Pen(ApplyFadeAlpha(LandingMarkerColor, AlphaMultiplier), 2.5f))
            {
                GraphicsSurface.DrawEllipse(
                    landingRingPen,
                    LandingScreenPosition.X - LandingRingRadius,
                    LandingScreenPosition.Y - LandingRingRadius,
                    LandingRingRadius * 2f,
                    LandingRingRadius * 2f);
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