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
/// Event data for <see cref="GarminR10Client.Disconnected"/>.
/// </summary>
public sealed class GarminR10DisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// The paired Bluetooth device name that was disconnected from.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Whether <see cref="GarminR10Client"/> will automatically attempt to reconnect.
    /// </summary>
    public bool WillAutoReconnect { get; }

    /// <summary>
    /// Initializes the event args for a disconnect.
    /// </summary>
    /// <param name="DeviceName">The paired Bluetooth device name that was disconnected from.</param>
    /// <param name="WillAutoReconnect">Whether an automatic reconnect attempt will follow.</param>
    public GarminR10DisconnectedEventArgs(string DeviceName, bool WillAutoReconnect)
    {
        this.DeviceName = DeviceName;
        this.WillAutoReconnect = WillAutoReconnect;
    }
}