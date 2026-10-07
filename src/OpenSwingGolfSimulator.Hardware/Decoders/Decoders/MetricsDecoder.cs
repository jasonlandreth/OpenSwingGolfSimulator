// Copyright 2026 Jason Landreth
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
 
using LaunchMonitor.Proto;
using OpenSwingGolfSimulator.Shared.Models;
using OpenSwingGolfSimulator.Shared.Enums; 
using static LaunchMonitor.Proto.Metrics.Types;

namespace OpenSwingGolfSimulator.Hardware.Decoders.Decoders;

/// <summary>
/// Default <see cref="IMetricsDecoder"/> implementation. Converts raw <see cref="Metrics"/>
/// protobuf messages, as received from the Garmin R10, into the simpler <see cref="ShotDataTest"/>/
/// <see cref="BallDataTest"/>/<see cref="ClubDataTest"/>/<see cref="SwingDataTest  "/> shapes used throughout
/// the rest of the app.
/// </summary>
public sealed class MetricsDecoder : IMetricsDecoder
{
    /// <summary>
    /// Conversion factor from meters per second to miles per hour.
    /// </summary>
    public const float MpsToMph = 2.23694f;

    /// <summary>
    /// Decodes a raw <see cref="Metrics"/> message into a <see cref="ShotData"/> instance.
    /// </summary>
    /// <param name="Metrics">The raw metrics message received from the device.</param>
    /// <returns>The decoded shot data.</returns>
    public ShotData Decode(Metrics Metrics)
    {
        BallData? ball = null;
        if (Metrics.BallMetrics is { } b)
        {
            float spinAxis = b.HasSpinAxis ? b.SpinAxis : 0f;
            float total = b.HasTotalSpin ? b.TotalSpin : 0f;
            float axisRad = spinAxis * MathF.PI / 180f;

            ball = new BallData
            {
                VerticalLaunchAngle = b.HasLaunchAngle ? b.LaunchAngle : 0f,
                HorizontalLaunchAngle = b.HasLaunchDirection ? b.LaunchDirection : 0f,
                BallSpeed = b.HasBallSpeed ? b.BallSpeed * MpsToMph : 0f,
                SpinAxis = spinAxis,
                TotalSpin = total,
                BackSpin = total * MathF.Cos(axisRad),
                SideSpin = total * MathF.Sin(axisRad),
                SpinCalcType = b.HasSpinCalculationType
                  ? b.SpinCalculationType switch
                  {
                      BallMetrics.Types.SpinCalculationType.Ratio => SpinCalcType.Ratio,
                      BallMetrics.Types.SpinCalculationType.BallFlight => SpinCalcType.BallFlight,
                      BallMetrics.Types.SpinCalculationType.Measured => SpinCalcType.Measured,
                      _ => SpinCalcType.Other,
                  }
                  : SpinCalcType.Ratio,
            };
        }

        ClubData? club = null;
        if (Metrics.ClubMetrics is { } c)
        {
            club = new ClubData
            {
                ClubHeadSpeed = c.HasClubHeadSpeed ? c.ClubHeadSpeed * MpsToMph : 0f,
                FaceToTarget = c.HasClubAngleFace ? c.ClubAngleFace : 0f,
                PathToTarget = c.HasClubAnglePath ? c.ClubAnglePath : 0f,
                AttackAngle = c.HasAttackAngle ? c.AttackAngle : 0f,
            };
        }


        SwingData? swing = null;
        if (Metrics.SwingMetrics is { } s)
        {
            swing = new SwingData
            {
                BackswingStart = s.HasBackSwingStartTime ? s.BackSwingStartTime : 0u,
                DownswingStart = s.HasDownSwingStartTime ? s.DownSwingStartTime : 0u,
                Impact = s.HasImpactTime ? s.ImpactTime : 0u,
                FollowThroughEnd = s.HasFollowThroughEndTime ? s.FollowThroughEndTime : 0u,
            };
        }

        return new ShotData
        {
            ShotId = Metrics.HasShotId ? Metrics.ShotId : 0u,
            ShotType = Metrics.HasShotType && Metrics.ShotType == Metrics.Types.ShotType.Normal
              ? OpenSwingGolfSimulator.Shared.Enums.ShotType.Normal : OpenSwingGolfSimulator.Shared.Enums.ShotType.Practice,
            Ball = ball,
            Club = club,
            Swing = swing,
        };
    }

    ShotData IMetricsDecoder.Decode(Metrics Metrics)
    {
        return Decode(Metrics);
    }
}
