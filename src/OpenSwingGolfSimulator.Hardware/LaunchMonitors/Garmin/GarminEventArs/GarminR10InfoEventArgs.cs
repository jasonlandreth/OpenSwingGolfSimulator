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
/// Event data for <see cref="GarminR10Client.Info"/>.
/// </summary>
public sealed class GarminR10InfoEventArgs : EventArgs
{
    /// <summary>
    /// A human-readable description of the step in progress.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes the event args with the info message.
    /// </summary>
    /// <param name="Message">A human-readable description of the step in progress.</param>
    public GarminR10InfoEventArgs(string Message)
    {
        this.Message = Message;
    }
}