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

namespace OpenSwingGolfSimulator.Hardware.LaunchMonitors.Garmin.GarminEventArgs;

/// <summary>
/// Event data for <see cref="GarminR10Client.ReadyChanged"/>.
/// </summary>
public sealed class GarminR10ReadyEventArgs : EventArgs
{
    /// <summary>
    /// Whether the device is currently ready to record a shot.
    /// </summary>
    public bool Ready { get; }

    /// <summary>
    /// Initializes the event args with the readiness state.
    /// </summary>
    /// <param name="Ready">Whether the device is currently ready to record a shot.</param>
    public GarminR10ReadyEventArgs(bool Ready)
    {
        this.Ready = Ready;
    }
}