
using OpenSwingGolfSimulator.Shared.Models;
using System.ComponentModel; 
using System.Drawing.Drawing2D;  

namespace OpenSwingGolfSimulator.UI.Controls
{
    public partial class ShotTracerControl : UserControl
    {
        private List<Vector3D> trajectoryPoints = new List<Vector3D>();
        private bool isBallActive = false;
        private float flightProgress = 0f;
        private int totalFrames = 120;

        // Shot physics parameters (can be updated dynamically from a launch monitor feed)
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public float LaunchAngle { get; set; } = 25f;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public float SideSpin { get; set; } = -500f; // negative = draw/hook, positive = fade/slice
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float MaxDistance { get; set; } = 200f; // yards 
        [Category("Trajectory")]
        [Description("Controls how high the shot appears to travel visually.")]
        [DefaultValue(4.0f)]
        public float VisualHeightMultiplier { get; set; } = 4.0f; 

        [Category("Trajectory")]
        [Description("Controls the vertical visual scale of the golf shot.")]
        [DefaultValue(40f)]
        public float VisualHeightScale { get; set; } = 40f;
        public ShotTracerControl()
        {
            InitializeComponent();

            // Enable double buffering to eliminate flicker during rendering
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
        }

        public void HitShot()
        {
            trajectoryPoints.Clear();
            flightProgress = 0f;
            isBallActive = true;
            if (!animationTimer.Enabled)
                animationTimer.Start();
        }

        //private void animationTimer_Tick(object sender, EventArgs e)
        //{
        //    if (!isBallActive) return;

        //    flightProgress += 1f / totalFrames;
        //    if (flightProgress > 1f)
        //    {
        //        flightProgress = 1f;
        //        isBallActive = false;
        //        animationTimer.Stop();
        //    }

        //    float t = flightProgress;
        //    float currentDist = t * MaxDistance;

        //    // Parabolic height calculation mimicking real arc
        //    float currentHeight = (float)(Math.Sin(t * Math.PI) * (MaxDistance * 0.35 * (LaunchAngle / 30f)));
        //    if (t >= 1f) currentHeight = 0f;

        //    // Side deviation profile curve
        //    float currentSide = (float)((Math.Pow(t, 2) * (SideSpin / 100.0)) + (t * 10.0));

        //    // 2.5D Perspective Projection scaling math
        //    float vanishingPointX = this.Width / 2f;
        //    float vanishingPointY = this.Height * 0.45f;
        //    float scale = 300f / (300f + currentDist * 2f);

        //    float screenX = vanishingPointX + (currentSide * scale * 15f);
        //    float screenY = vanishingPointY + (currentDist * 0.8f) -(currentHeight * VisualHeightScale);

        //    trajectoryPoints.Add(new TrajectoryPoint3D { X = screenX, Y = screenY, Z = scale });

        //    // Keep trail length trimmed to maintain the "ribbon" look
        //    if (trajectoryPoints.Count > 40)
        //    {
        //        trajectoryPoints.RemoveAt(0);
        //    }

        //    this.Invalidate(); // Refresh control surface
        //}
        private void animationTimer_Tick(object sender,System.EventArgs e)
        {
            if (!isBallActive)
            {
                return;
            }

            flightProgress += 1f / totalFrames;

            if (flightProgress > 1f)
            {
                flightProgress = 1f;
                isBallActive = false;
                animationTimer.Stop();
            }

            float t = flightProgress;

            // Distance traveled downrange.
            float currentDist = t * MaxDistance;

            // Calculate the visual height of the ball.
            // The sine curve creates a natural rise and fall.
            float currentHeight = (float)(
                Math.Sin(t * Math.PI) *
                (MaxDistance * 0.35f *
                (LaunchAngle / 30f) *
                VisualHeightMultiplier));

            if (t >= 1f)
            {
                currentHeight = 0f;
            }

            // Calculate the amount of lateral movement caused by side spin.
            float currentSide = (float)(
                (Math.Pow(t, 2) * (SideSpin / 100.0)) +
                (t * 10.0));

            // Vanishing point / horizon.
            float vanishingPointX = this.Width / 2f;
            float vanishingPointY = this.Height * 0.45f;

            // Perspective scaling makes the ball appear smaller as it travels
            // farther away.
            float scale =
                300f /
                (300f + currentDist * 2f);

            // Calculate horizontal screen position.
            float screenX =
                vanishingPointX +
                (currentSide * scale * 15f);

            // Calculate vertical screen position.
            //
            // IMPORTANT:
            // The height is NOT multiplied by the perspective scale.
            // This allows VisualHeightScale and VisualHeightMultiplier
            // to have a much stronger effect on the visible trajectory.
            float screenY =
                vanishingPointY +
                (currentDist * 0.8f) -
                (currentHeight * VisualHeightScale);

            trajectoryPoints.Add(
                new Vector3D
                {
                    X = screenX,
                    Y = screenY,
                    Z = scale
                });

            // Keep the most recent trajectory points for the tracer.
            if (trajectoryPoints.Count > 40)
            {
                trajectoryPoints.RemoveAt(0);
            }

            this.Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float vanishingPointY = this.Height * 0.45f;

            // Render Sky backdrop
            using (SolidBrush skyBrush = new SolidBrush(Color.FromArgb(92, 147, 196)))
            {
                g.FillRectangle(skyBrush, 0, 0, this.Width, vanishingPointY);
            }

            // Render Fairway Turf backdrop
            using (SolidBrush turfBrush = new SolidBrush(Color.FromArgb(51, 77, 38)))
            {
                g.FillRectangle(turfBrush, 0, vanishingPointY, this.Width, this.Height - vanishingPointY);
            }

            // Render Tracer Ribbon tail
            if (trajectoryPoints.Count > 1)
            {
                for (int i = 1; i < trajectoryPoints.Count; i++)
                {
                    var p1 = trajectoryPoints[i - 1];
                    var p2 = trajectoryPoints[i];

                    float alphaFloat = (float)i / trajectoryPoints.Count;
                    int alpha = Math.Max(20, Math.Min(255, (int)(alphaFloat * 255)));

                    using (Pen tracePen = new Pen(Color.FromArgb(alpha, 255, 20, 147), Math.Max(1f, (int)p2.Z * 12f)))
                    {
                        tracePen.StartCap = LineCap.Round;
                        tracePen.EndCap = LineCap.Round;
                        g.DrawLine(tracePen, (int)p1.X, (int)p1.Y, (int)p2.X, (int)p2.Y);
                    }
                }
            }

            // Render Active Golf Ball
            if (isBallActive && trajectoryPoints.Count > 0)
            {
                var currentPos = trajectoryPoints[trajectoryPoints.Count - 1];
                float ballSize = Math.Max(4f, (int)currentPos.Z * 12f);

                using (SolidBrush ballBrush = new SolidBrush(Color.White))
                using (Pen ballPen = new Pen(Color.Black, 1f))
                {
                    g.FillEllipse(ballBrush, (int)(currentPos.X - ballSize / 2), (int)(currentPos.Y - ballSize / 2), (int)ballSize, (int)ballSize);
                    g.DrawEllipse(ballPen, (int)(currentPos.X - ballSize / 2), (int)(currentPos.Y - ballSize / 2), (int)ballSize, (int)ballSize);
                }
            }
        }

 
    }
} 