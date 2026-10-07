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


namespace OpenSwingGolfSimulator.Shared.Models
{

    /// <summary>
    /// Club delivery metrics for a single shot.
    /// </summary>
    public sealed class ClubData
    {
        /// <summary>
        /// Club head velocity at impact (mph).
        /// </summary>
        public float ClubHeadSpeed { get; init; }

        /// <summary>
        /// Face angle at impact (degrees).
        /// </summary>
        public float FaceToTarget { get; init; }

        /// <summary>
        /// Direction club face is pointing at impact (+ Open, - Closed)
        /// </summary>
        public double FaceAngle { get; set; }

        /// <summary>
        /// Club path angle (degrees).
        /// </summary>
        public float PathToTarget { get; init; }


        /// <summary>
        /// Direction of club head motion at impact (+ In-to-Out, - Out-to-In)
        /// </summary>
        public double ClubPath => PathToTarget;

        /// <summary>
        /// Angle of attack (degrees).
        /// </summary>
        public float AttackAngle { get; init; }

        /// <summary>
        /// The difference between the face angle and the path angle (degrees).
        /// Difference between Face Angle and Club Path in degrees
        /// </summary>
        public double FaceToPath => FaceToTarget - PathToTarget;

        /// <summary>
        /// Effective loft on the club face at impact in degrees
        /// </summary>
        public double DynamicLoft { get; set; }

        /// <summary>
        /// Impact location relative to center (+ Toe, - Heel in mm)
        /// </summary>
        public double ImpactLocationX { get; set; }

        /// <summary>
        /// Impact location relative to center (+ High, - Low in mm)
        /// </summary>
        public double ImpactLocationY { get; set; }


        /// <summary>
        /// Automatically calculates Smash Factor, Spin Axis, and Face-to-Path if not provided directly by launch monitor.
        /// </summary>
        public void RecalculateDerivedMetrics()
        {

            //TODO Implement this logic in a utlity class so the BallData ban be used.
            //if (ClubHeadSpeed > 0)
            //{
            //    SmashFactor = BallSpeed / ClubHeadSpeed;
            //}

            //FaceToPath = FaceAngle - ClubPath;

            //// Calculate Spin Axis from BackSpin and SideSpin if SpinAxis is zero
            //if (SpinAxis == 0 && (BackSpin != 0 || SideSpin != 0))
            //{
            //    SpinAxis = Math.Atan2(SideSpin, BackSpin) * (180.0 / Math.PI);
            //}
        }
    }

}
