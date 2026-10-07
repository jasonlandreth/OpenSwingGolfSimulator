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


using OpenSwingGolfSimulator.Shared.Models;

namespace OpenSwingGolfSimulator.Hardware.LaunchMonitors.Garmin.GarminEventArgs;

/// <summary>
/// Event data for <see cref="GarminR10Client.ShotReceived"/>.
/// </summary>
public sealed class GarminR10ShotEventArgs : EventArgs
{
    /// <summary>
    /// The decoded shot data.
    /// </summary>
    public ShotData Shot { get; }

    /// <summary>
    /// Initializes the event args with the decoded shot.
    /// </summary>
    /// <param name="Shot">The decoded shot data.</param>
    public GarminR10ShotEventArgs(ShotData Shot)
    {
        this.Shot = Shot;
    }
}
