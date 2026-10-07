using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace OpenSwingGolfSimulator.Physics.Models
{
    /// <summary>
    /// Bundles the screen-space geometry and readout values needed to paint a single animation
    /// frame of an in-progress golf shot tracer. Rebuilt every time
    /// <see cref="GolfShotTracerControl"/> repaints while a shot is animating.
    /// </summary>
    public sealed class TracerFrameGeometry
    {
        /// <summary>
        /// The points, in control-relative screen coordinates, making up the airborne portion of
        /// the tracer arc drawn so far, ordered from the tee to the ball's current position (or
        /// to the landing point, once the ball has landed).
        /// </summary>
        public List<PointF> FlightArcScreenPoints { get; } = new List<PointF>();

        /// <summary>
        /// The points, in control-relative screen coordinates, making up the ground shadow drawn
        /// directly beneath the flight arc so far, one per entry in
        /// <see cref="FlightArcScreenPoints"/>, flattened to the ground plane.
        /// </summary>
        public List<PointF> GroundShadowScreenPoints { get; } = new List<PointF>();

        /// <summary>
        /// The points, in control-relative screen coordinates, making up the ground-roll segment
        /// drawn so far after the ball has landed. Empty until the animation progresses past the
        /// end of the flight phase.
        /// </summary>
        public List<PointF> RollScreenPoints { get; } = new List<PointF>();

        /// <summary>
        /// The current on-screen position of the ball for this animation frame, used to anchor
        /// the ball marker and the floating live-yardage callout.
        /// </summary>
        public PointF CurrentBallScreenPosition { get; set; }

        /// <summary>
        /// The downrange distance the ball has traveled so far in this animation frame, in yards.
        /// </summary>
        public double CurrentDownrangeYards { get; set; }

        /// <summary>
        /// True once the animation has progressed past the trajectory's highest point, at which
        /// point the apex marker and callout should be drawn and held in place for the rest of
        /// the animation.
        /// </summary>
        public bool HasPassedApex { get; set; }

        /// <summary>
        /// The on-screen position of the apex of the flight arc. Only meaningful when
        /// <see cref="HasPassedApex"/> is true.
        /// </summary>
        public PointF ApexScreenPosition { get; set; }

        /// <summary>
        /// The peak height of the shot, in feet, as reported in the shot record. Displayed in the
        /// apex callout once <see cref="HasPassedApex"/> is true.
        /// </summary>
        public double ApexHeightFeet { get; set; }

        /// <summary>
        /// True once the ball has reached the final point of <see cref="GolfShotTrajectoryData.CarryTrajectoryPoints"/>,
        /// at which point the landing marker should be drawn and held in place for the rest of
        /// the animation.
        /// </summary>
        public bool HasLanded { get; set; }

        /// <summary>
        /// The on-screen position where the ball first touched the ground. Only meaningful when
        /// <see cref="HasLanded"/> is true.
        /// </summary>
        public PointF LandingScreenPosition { get; set; }

        /// <summary>
        /// True once the animation has finished (the ball has come to rest at the shot's total
        /// distance), at which point the control should stop the animation timer.
        /// </summary>
        public bool IsAnimationComplete { get; set; }
    }

}
