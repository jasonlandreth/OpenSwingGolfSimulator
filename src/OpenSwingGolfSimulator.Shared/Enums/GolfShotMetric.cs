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


using OpenSwingGolfSimulator.Shared.Attributes;

namespace OpenSwingGolfSimulator.Shared.Enums;

public enum GolfShotMetric
{
    #region Ball Data

    [GolfShot("Ball speed in miles per hour", "mph", "Ball Speed")]
    BallSpeed,

    [GolfShot("Efficiency ratio of ball speed to club speed", "mph", "Smash Factor")]
    SmashFactor,

    [GolfShot("Carry distance in yards", "yds", "Carry Distance")]
    CarryDistance,

    [GolfShot("Total distance including roll in yards", "yds", "Total Distance")]
    TotalDistance,

    [GolfShot("Distance rolled after landing in yards", "yds", "Roll Distance")]
    RollDistance,

    [GolfShot("Vertical launch angle in degrees relative to horizontal", "deg", "Vertical Launch Angle")]
    VerticalLaunchAngle,

    [GolfShot("Horizontal launch angle (HLA) in degrees (+ Right, - Left)", "deg", "Horizontal Launch Direction")]
    HorizontalLaunchAngle,

    [GolfShot("Total spin rate in revolutions per minute", "rpm", "Spin Rate")]
    SpinRate,

    [GolfShot("Backspin component (RPM). Computed: total_spin * cos(spin_axis)", "rpm", "Back Spin")]
    BackSpin,

    [GolfShot("Side spin. Computed: total_spin * sin(spin_axis) component in RPM (+ Right / Fade, - Left / Draw)",
        "rpm", "Side Spin")]
    SideSpin,

    [GolfShot("Spin axis tilt angle in degrees (-15° Draw, +15° Fade)", "deg", "Spin Axis")]
    SpinAxis,

    [GolfShot("Peak trajectory height in feet", "feet", "Apex Height")]
    ApexHeight,

    [GolfShot("Total time of flight in seconds", "duration", "Flight Time")]
    FlightTime,

    [GolfShot("Offline distance from target line in yards (+ Right, - Left)", "yds", "Lateral Offline")]
    LateralOffline,

    [GolfShot("Spin calculation method used to derive spin metrics.", "", "Spin Calculation Type")]
    SpinCalcType,

    #endregion

    #region Club Data

    [GolfShot("Club head speed at impact in miles per hour", "mph", "Club Head Speed")]
    ClubHeadSpeed,

    [GolfShot("Direction of club head motion at impact(Club Path) (+ In-to-Out, - Out-to-In)", "deg", "Club Path")]
    ClubPath,

    [GolfShot("Direction club face(FaceToTarget-Face angle at impact) is pointing at impact (+ Open, - Closed). Also called Face Angle",
        "deg", "Face To Target")]
    FaceToTarget,

    [GolfShot("The difference between the face angle(Face to Target and the path angle(Path to Target)", "deg", "Face To Path")]
    FaceToPath,

    [GolfShot("Vertical angle of attack club head motion (+ Ascending, - Descending)", "deg", "Attack Angle")]
    AttackAngle,

    #endregion

    #region SwingData

    [GolfShot("The timestamp when the backswing starts, in milliseconds since the device started", "duration", "Backswing Start")]
    BackswingStart,

    [GolfShot("The timestamp when the downswing starts, in milliseconds since the device started", "duration", "Downswing Start")]
    DownswingStart,

    [GolfShot("The timestamp when the impact occurs, in milliseconds since the device started", "duration", "Impact")]
    Impact,

    [GolfShot("The timestamp when the follow-through ends, in milliseconds since the device started", "duration", "Follow Through End")]
    FollowThroughEnd,

    [GolfShot("Effective loft on the club face at impact in degrees", "deg", "Dynamic Loft")]
    DynamicLoft,

    [GolfShot("Impact location relative to center (+ Toe, - Heel in mm)", "duration", "Impact Location X")]
    ImpactLocationX,

    [GolfShot("Impact location relative to center (+ High, - Low in mm)", "duration", "Impact Location Y")]
    ImpactLocationY

    #endregion
}
