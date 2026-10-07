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
/// Club delivery metrics for a single shot.
/// </summary>
public sealed class ClubDataTest
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
    /// </summary>
    public double FaceToPath => FaceToTarget - PathToTarget;
    /// <summary>
    /// Effective loft on the club face at impact in degrees
    /// </summary>
    public double DynamicLoft { get; set; }

}
