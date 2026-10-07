/*
 * Copyright (c) 2026 Jason Landreth
 *
 * Licensed under the Apache License, Version 2.0 (the ""License"");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an ""AS IS"" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
namespace OpenSwingGolf.Hardware.LaunchMonitors.Garmin
{
  public class GolfShotData
  {
    #region Ball Data

    /// <summary>
    /// Ball speed in miles per hour (mph)
    /// </summary>
    public double BallSpeed { get; set; }

    /// <summary>
    /// Efficiency ratio of ball speed to club speed
    /// </summary>
    public double SmashFactor { get; set; }

    /// <summary>
    /// Carry distance in yards
    /// </summary>
    public double CarryDistance { get; set; }

    /// <summary>
    /// Total distance including roll in yards
    /// </summary>
    public double TotalDistance { get; set; }

    /// <summary>
    /// Distance rolled after landing in yards
    /// </summary>
    public double RollDistance { get; set; }

    /// <summary>
    /// Vertical launch angle in degrees relative to horizontal
    /// </summary>
    public double LaunchAngle { get; set; }

    /// <summary>
    /// Horizontal launch angle (HLA) in degrees (+ Right, - Left)
    /// </summary>
    public double HorizontalLaunchAngle { get; set; }

    /// <summary>
    /// Total spin rate in revolutions per minute (RPM)
    /// </summary>
    public double SpinRate { get; set; }

    /// <summary>
    /// BackSpin component in RPM
    /// </summary>
    public double BackSpin { get; set; }

    /// <summary>
    /// Side spin component in RPM (+ Right / Fade, - Left / Draw)
    /// </summary>
    public double SideSpin { get; set; }

    /// <summary>
    /// Spin axis tilt angle in degrees (-15° Draw, +15° Fade)
    /// </summary>
    public double SpinAxis { get; set; }

    /// <summary>
    /// Peak trajectory height in feet
    /// </summary>
    public double ApexHeight { get; set; }

    /// <summary>
    /// Total time of flight in seconds
    /// </summary>
    public double FlightTime { get; set; }

    /// <summary>
    /// Offline distance from target line in yards (+ Right, - Left)
    /// </summary>
    public double LateralOffline { get; set; }

    #endregion

    #region Club Data

    /// <summary>
    /// Club head speed at impact in miles per hour (mph)
    /// </summary>
    public double ClubSpeed { get; set; }

    /// <summary>
    /// Direction of club head motion at impact (+ In-to-Out, - Out-to-In)
    /// </summary>
    public double ClubPath { get; set; }

    /// <summary>
    /// Direction club face is pointing at impact (+ Open, - Closed)
    /// </summary>
    public double FaceAngle { get; set; }
    

    /// <summary>
    /// Difference between Face Angle and Club Path in degrees
    /// </summary>
    public double FaceToPath { get; set; }

    /// <summary>
    /// Vertical angle of club head motion (+ Ascending, - Descending)
    /// </summary>
    public double AttackAngle { get; set; }

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

    #endregion

    #region Helper Methods

    /// <summary>
    /// Automatically calculates Smash Factor, Spin Axis, and Face-to-Path if not provided directly by launch monitor.
    /// </summary>
    public void RecalculateDerivedMetrics()
    {
      if (ClubSpeed > 0)
      {
        SmashFactor = BallSpeed / ClubSpeed;
      }

      FaceToPath = FaceAngle - ClubPath;

      // Calculate Spin Axis from BackSpin and SideSpin if SpinAxis is zero
      if (SpinAxis == 0 && (BackSpin != 0 || SideSpin != 0))
      {
        SpinAxis = Math.Atan2(SideSpin, BackSpin) * (180.0 / Math.PI);
      }
    }

    #endregion
  }
}
