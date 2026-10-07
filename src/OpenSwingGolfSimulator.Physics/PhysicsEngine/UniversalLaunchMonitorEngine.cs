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

using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenSwingGolfSimulator.Physics.PhysicsEngine
{
    public enum SurfaceHardness { Soft, Medium, Hard, Concrete }
    public class UltimateLaunchMonitorEngine
    {
        // --- Core Internal Physics Properties (Strict Metric System SI Units) ---
        public Vector3 Position { get; private set; } // Tracks internally in Meters
        public Vector3 Velocity { get; private set; } // Tracks internally in m/s
        public float SpinRPM { get; private set; }
        public float SpinAxisTiltDegrees { get; private set; }

        public float Mass { get; set; } = 0.04593f;  // Golf ball mass (kg)
        public float Radius { get; set; } = 0.02135f; // Golf ball radius (m)

        // --- Environment & Air Constants ---
        public float TemperatureC { get; set; } = 20.0f;
        public float HumidityPercent { get; set; } = 50.0f;
        public float ElevationMeters { get; set; } = 0.0f;
        public Vector3 WindVelocity { get; set; } = Vector3.Zero; // m/s
        public SurfaceHardness GrassHardness { get; set; } = SurfaceHardness.Medium;

        // --- Telemetry Output (Returned cleanly to the view in YARDS) ---
        public List<Vector3> TrajectoryPointsYards { get; private set; } = new List<Vector3>();
        public float RecordIntervalSeconds { get; set; } = 0.005f;
        private float _timeSinceLastRecord = 0f;

        // --- Public Output Interface (Automatically handles property encapsulation conversions) ---
        public float CarryDistanceYards { get; private set; }
        public float TotalDistanceYards { get; private set; }
        public float CarryDeviationYards { get; private set; }
        public float TotalDeviationYards { get; private set; }
        public float ApexHeightFeet { get; private set; }
        public float TimeToApexSeconds { get; private set; }
        public float LandingAngleDegrees { get; private set; }
        public float CarryTimeSeconds { get; private set; }
        public float TotalTimeSeconds { get; private set; }

        private bool _hasHitApex;
        private bool _hasHitGround;
        private bool _isStopped;
        private float _computedAirDensity;
        private float _dynamicViscosity;

        public UltimateLaunchMonitorEngine(float BallSpeedMPH, float LaunchAngleDegrees, float LaunchAzimuthDegrees, float TotalSpinRPM, float SpinAxisTiltDegrees)
        {
            // Clean boundary conversion: Radar inputs map straight to pure metric values
            float speedMS = BallSpeedMPH * 0.44704f;
            float launchRad = LaunchAngleDegrees * (float)Math.PI / 180f;
            float azimuthRad = LaunchAzimuthDegrees * (float)Math.PI / 180f;

            float velY = speedMS * (float)Math.Sin(launchRad);
            float horizontalSpeed = speedMS * (float)Math.Cos(launchRad);
            float velX = horizontalSpeed * (float)Math.Cos(azimuthRad);
            float velZ = horizontalSpeed * (float)Math.Sin(azimuthRad);

            // Set metric initial state positioning
            Position = new Vector3(0f, Radius, 0f);
            Velocity = new Vector3(velX, velY, velZ);

            SpinRPM = TotalSpinRPM;
            this.SpinAxisTiltDegrees = SpinAxisTiltDegrees; // Handled 'this.' shadowing fix
        }

        public void RunSimulation(float DeltaTimeSeconds = 0.0005f)
        {
            float tempK = TemperatureC + 273.15f;
            _computedAirDensity = CalculateAirDensity(TemperatureC, HumidityPercent, ElevationMeters);
            _dynamicViscosity = (float)(1.458e-6 * Math.Pow(tempK, 1.5) / (tempK + 110.4));

            _hasHitApex = false; _hasHitGround = false; _isStopped = false;
            ApexHeightFeet = 0; TotalTimeSeconds = 0; _timeSinceLastRecord = 0f;

            TrajectoryPointsYards.Clear();
            TrajectoryPointsYards.Add(Position * 1.09361f); // Save first yard entry node

            // Fire 2000Hz pure mathematical metric steps
            while (!_isStopped && TotalTimeSeconds < 25.0f)
            {
                ExecutePhysicsTimeStep(DeltaTimeSeconds);
            }

            System.Diagnostics.Debug.WriteLine($"CALCULATED TOTAL DISTANCE: {TotalDistanceYards:F1} Yards");
        }

        private void ExecutePhysicsTimeStep(float DeltaTimeSeconds)
        {
            if (_isStopped) return;
            TotalTimeSeconds += DeltaTimeSeconds;
            _timeSinceLastRecord += DeltaTimeSeconds;

            // 1. DYNAMIC SPIN DECAY
            SpinRPM *= (1.0f - (0.038f * DeltaTimeSeconds));
            float spinRadSec = (SpinRPM * 2f * (float)Math.PI) / 60f;

            // 2. FLUID DYNAMICS (Meters per second flow)
            Vector3 relativeVelocity = Velocity - WindVelocity;
            float relativeSpeed = relativeVelocity.Length();

            Vector3 dragForce = Vector3.Zero;
            Vector3 magnusForce = Vector3.Zero;

            if (relativeSpeed > 0.001f)
            {
                float crossSectionArea = (float)(Math.PI * Radius * Radius);
                Vector3 relativeVelocityNorm = Vector3.Normalize(relativeVelocity);

                float spinParameter = (spinRadSec * Radius) / relativeSpeed;
                float Cd = 0.22f + (0.10f * spinParameter);
                float Cl = 0.05f + (0.28f * (float)Math.Pow(spinParameter, 0.4));

                if (Position.Y < Radius * 5f && Position.Y > Radius)
                {
                    Cl *= 1.20f;
                }

                float dragMagnitude = 0.5f * _computedAirDensity * (relativeSpeed * relativeSpeed) * Cd * crossSectionArea;
                dragForce = -relativeVelocityNorm * dragMagnitude;

                // Quaternion lift vector tilt architecture
                Vector3 rightDir = Vector3.Normalize(Vector3.Cross(relativeVelocityNorm, Vector3.UnitY));
                Vector3 pureLiftDir = Vector3.Normalize(Vector3.Cross(rightDir, relativeVelocityNorm));

                float tiltRad = SpinAxisTiltDegrees * (float)Math.PI / 180f;
                Quaternion tiltRotation = Quaternion.CreateFromAxisAngle(relativeVelocityNorm, -tiltRad);
                Vector3 dynamicLiftDirection = Vector3.Transform(pureLiftDir, tiltRotation);

                float magnusMagnitude = 0.5f * _computedAirDensity * (relativeSpeed * relativeSpeed) * Cl * crossSectionArea;
                magnusForce = dynamicLiftDirection * magnusMagnitude;
            }

            // 3. STEP METRIC KINEMATICS (Pure integration loop)
            Vector3 gravityForce = new Vector3(0, -9.81f, 0) * Mass;
            Vector3 netForce = gravityForce + dragForce + magnusForce;
            Vector3 acceleration = netForce / Mass;

            Velocity += acceleration * DeltaTimeSeconds;
            Position += Velocity * DeltaTimeSeconds;

            // Record trajectory point array scale cleanly converted to display Yards
            if (_timeSinceLastRecord >= RecordIntervalSeconds)
            {
                TrajectoryPointsYards.Add(Position * 1.09361f);
                _timeSinceLastRecord = 0f;
            }

            // Monitor flight apex
            if (!_hasHitApex && Velocity.Y < 0.0f)
            {
                _hasHitApex = true;
                ApexHeightFeet = Position.Y * 3.28084f; // Meters to Feet conversion
                TimeToApexSeconds = TotalTimeSeconds;
            }

            // 4. RADAR GROUND TRACE RESOLUTION (Only intercept when descending)
            if (Velocity.Y < 0f && Position.Y - Radius <= 0f)
            {
                Position = new Vector3(Position.X, Radius, Position.Z);

                if (!_hasHitGround)
                {
                    _hasHitGround = true;
                    CarryDistanceYards = Position.X * 1.09361f;   // Meters to Yards
                    CarryDeviationYards = Position.Z * 1.09361f;  // Meters to Yards
                    CarryTimeSeconds = TotalTimeSeconds;
                    LandingAngleDegrees = (float)(Math.Atan2(-Velocity.Y, Velocity.X) * 180f / Math.PI);
                }

                float bounceElasticity = 0.40f;
                float rollingFriction = 0.22f;

                if (GrassHardness == SurfaceHardness.Hard) { bounceElasticity = 0.55f; rollingFriction = 0.14f; }
                else if (GrassHardness == SurfaceHardness.Soft) { bounceElasticity = 0.15f; rollingFriction = 0.40f; }

                if (Math.Abs(Velocity.Y) > 0.35f)
                {
                    float angleDampingModifier = (float)Math.Cos(LandingAngleDegrees * Math.PI / 180f);
                    float newVelY = -Velocity.Y * bounceElasticity;

                    float forwardGearEffect = (spinRadSec * Radius) * 0.12f;
                    float newVelX = (Velocity.X * (1f - rollingFriction) * angleDampingModifier) + forwardGearEffect;
                    float newVelZ = Velocity.Z * (1f - rollingFriction);

                    Velocity = new Vector3(newVelX, newVelY, newVelZ);
                    SpinRPM *= 0.40f;
                    TrajectoryPointsYards.Add(Position * 1.09361f);
                }
                else
                {
                    // Pure rollout execution tracking state
                    if (Velocity.Length() > 0.15f)
                    {
                        Velocity = new Vector3(Velocity.X * (1f - rollingFriction), 0f, Velocity.Z * (1f - rollingFriction));
                    }
                    else
                    {
                        Velocity = Vector3.Zero;
                        _isStopped = true;

                        // Terminal outputs locked down directly to display fields
                        TotalDistanceYards = Position.X * 1.09361f;
                        TotalDeviationYards = Position.Z * 1.09361f;
                        TrajectoryPointsYards.Add(Position * 1.09361f);
                    }
                }
            }
        }
        private float CalculateAirDensity(float tempC, float humidityPct, float elevationM)
        {
            double standardPressureSeaLevel = 101325.0;
            double pressure = standardPressureSeaLevel * Math.Pow(1.0 - (0.0065 * elevationM / 288.15), 5.25588);
            double tempK = tempC + 273.15;
            double saturationVaporPressure = 610.78 * Math.Pow(10, (7.5 * tempC) / (tempC + 237.3));
            double actualVaporPressure = saturationVaporPressure * (humidityPct / 100.0);
            return (float)(((pressure - actualVaporPressure) / (287.058 * tempK)) + (actualVaporPressure / (461.495 * tempK)));
        }
    }
}


