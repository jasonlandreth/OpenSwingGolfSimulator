/*
 * Copyright 2026 Jason Landreth, Open Swing Golf
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


namespace OpenSwingGolfSimulator.Hardware.Decoders.DecodeTest;

/// <summary>
/// Ball flight metrics for a single shot.
/// </summary>
public sealed class BallDataTest
{
    /// <summary>
    /// Vertical launch angle (degrees).
    /// </summary>
    public float VerticalLaunchAngle { get; init; }

    /// <summary>
    /// Horizontal launch direction (degrees).
    /// </summary>
    public float HorizontalLaunchDirection { get; init; }

    /// <summary>
    /// Initial ball velocity (mph).
    /// </summary>
    public float BallSpeed { get; init; }

    /// <summary>
    /// Spin axis tilt (degrees).
    /// </summary>
    public float SpinAxis { get; set; }

    /// <summary>
    /// Total spin rate (RPM).
    /// </summary>
    public float TotalSpin { get; init; }

    /// <summary>
    /// Backspin component (RPM). Computed: total_spin * cos(spin_axis).
    /// </summary>
    public float BackSpin { get; init; }

    /// <summary>
    /// Sidespin component (RPM). Computed: total_spin * sin(spin_axis).
    /// </summary>
    public float SideSpin { get; init; }

    /// <summary>
    /// Spin calculation method used to derive spin metrics.
    /// </summary>
    public SpinCalcTypeTest SpinCalcType { get; init; }
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

    /// <summary>
    /// Efficiency ratio of ball speed to club speed
    /// </summary>
    public double SmashFactor { get; set; }

    /// <summary>
    /// Automatically calculates Smash Factor, Spin Axis, and Face-to-Path if not provided directly by launch monitor.
    /// </summary>
    public void RecalculateMetrics(float ClubHeadSpeed)
    {
        if (ClubHeadSpeed > 0)
        {
            SmashFactor = BallSpeed / ClubHeadSpeed;
        }
         
        // Calculate Spin Axis from BackSpin and SideSpin if SpinAxis is zero
        if (SpinAxis == 0 && (BackSpin != 0 || SideSpin != 0))
        {
            SpinAxis = (float)(Math.Atan2(SideSpin, BackSpin) * (180.0 / Math.PI));
        }
    }

}
