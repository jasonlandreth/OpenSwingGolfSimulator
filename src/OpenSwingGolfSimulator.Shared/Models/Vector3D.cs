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



/// Represents a single sampled point along a golf ball's simulated flight
/// path, expressed entirely in world-space yards so the values line up
/// naturally with the carry and total distance statistics that are already
/// reported in yards. A full shot is represented as an ordered list of these
/// samples, taken at increasing values of <see cref="ElapsedFlightTimeInSeconds"/>,
/// starting at the tee (time zero, all distances zero) and ending when the
/// ball comes to rest. The <see cref="DrivingRangeControl"/> control consumes
/// a list of these samples and projects each one onto the screen to draw the
/// tracer; it does not perform any physics simulation itself.
/// </summary>
namespace OpenSwingGolfSimulator.Shared.Models
{
    /// <summary>
    /// Simple 3D vector used to record trajectory points (X = lateral yards, Y = downrange yards, Z = height feet).
    /// Kept in Shared so multiple projects can reference it without circular project references.
    /// </summary>
    public struct Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
