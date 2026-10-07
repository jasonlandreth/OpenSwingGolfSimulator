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

 
using OpenSwingGolfSimulator.Domain.Models;
using OpenSwingGolfSimulator.Shared.Models;
using OpenSwingGolfSimulator.Shared.Enums;

namespace OpenSwingGolfSimulator.Physics.PhysicsEngine
{
    public class GolfBallPhysicsEngine
    {
        #region Physical Constants

        private const double BallMassKg = 0.04593;           // Standard golf ball weight (45.93g)
        private const double BallRadiusMeters = 0.021335;     // Standard golf ball radius (1.68 inches diameter)
        private const double CrossSectionArea = Math.PI * BallRadiusMeters * BallRadiusMeters;
        private const double Gravity = 9.80665;               // Standard acceleration due to gravity (m/s²)
        private const double MpsToMph = 2.23694;
        private const double MphToMps = 0.44704;
        private const double MetersToYards = 1.09361;
        private const double MetersToFeet = 3.28084;

        #endregion

        /// <summary>
        /// Calculates ball trajectory and landing metrics using Garmin R10 metrics and atmospheric conditions.
        /// </summary>
        public GolfTrajectoryResult CalculateTrajectory(ShotData shot, EnvironmentalConditions env = null)
        {
            env ??= new EnvironmentalConditions();

            GolfTrajectoryResult result = new GolfTrajectoryResult();
            result.ShotClassification = DetermineShotType(shot.Ball.HorizontalLaunchAngle, shot.Ball.SpinAxis, shot.Club.ClubPath);

            double airDensity = env.GetAirDensity();

            // 1. Initial State Conversions
            double v0 = shot.Ball.BallSpeed * MphToMps;
            double verticalRad = shot.Ball.VerticalLaunchAngle * (Math.PI / 180.0);
            double horizontalRad = shot.Ball.HorizontalLaunchAngle * (Math.PI / 180.0);

            // Velocity Components (Z = Downfield, X = Lateral, Y = Upward)
            double vz = v0 * Math.Cos(verticalRad) * Math.Cos(horizontalRad);
            double vx = v0 * Math.Cos(verticalRad) * Math.Sin(horizontalRad);
            double vy = v0 * Math.Sin(verticalRad);

            // Spin Components & Decay Setup
            double totalSpinRpm =shot.Ball.SpinRate;
            double spinAxisRad = shot.Ball.SpinAxis * (Math.PI / 180.0);

            // World Positions in Meters
            double px = 0.0;
            double py = 0.0;
            double pz = 0.0;

            // Integration Step Settings
            double dt = 0.001; // 1 ms resolution
            double time = 0.0;

            double peakHeightMeters = 0.0;
            bool hitGround = false;

            // Wind Setup in m/s
            double windMps = env.WindSpeedMph * MphToMps;
            double windRad = env.WindDirectionDegrees * (Math.PI / 180.0);
            double windX = windMps * Math.Sin(windRad);
            double windZ = -windMps * Math.Cos(windRad); // Headwind opposes Z

            // Trajectory Recording Interval (Every 20 ms)
            int sampleRate = 20;
            int stepCounter = 0;

            result.TrajectoryPoints.Add(new Vector3D(0, 0, 0));
            double currentSpinRpm = 0;
            // --- FLIGHT SIMULATION LOOP ---
            while (!hitGround && time < 15.0) // Safety cap at 15s flight time
            {
                time += dt;
                stepCounter++;

                // Relative Velocity relative to wind
                double relVx = vx - windX;
                double relVy = vy;
                double relVz = vz - windZ;
                double relSpeed = Math.Sqrt(relVx * relVx + relVy * relVy + relVz * relVz);

                // Spin Rate Decay over time (~1% loss per second)
                currentSpinRpm = totalSpinRpm * Math.Exp(-0.01 * time);
                double omega = currentSpinRpm * (2.0 * Math.PI / 60.0); // rad/s

                // Aerodynamic Coefficients
                double spinRatio = (omega * BallRadiusMeters) / (relSpeed > 0.1 ? relSpeed : 0.1);
                double cd = GetDragCoefficient(relSpeed, spinRatio);
                double cl = GetLiftCoefficient(spinRatio);

                // Forces Magnitude
                double dragForce = 0.5 * airDensity * CrossSectionArea * cd * relSpeed * relSpeed;
                double liftForce = 0.5 * airDensity * CrossSectionArea * cl * relSpeed * relSpeed;

                // Drag Vector Direction (Opposite to Relative Motion)
                double fDragX = -dragForce * (relVx / relSpeed);
                double fDragY = -dragForce * (relVy / relSpeed);
                double fDragZ = -dragForce * (relVz / relSpeed);

                // Magnus Lift Vector Axis Resolution
                // Spin axis tilts vector in X-Y and X-Z planes
                double liftDirX = -Math.Sin(spinAxisRad) * Math.Cos(verticalRad);
                double liftDirY = Math.Cos(spinAxisRad);
                double liftDirZ = -Math.Sin(spinAxisRad) * Math.Sin(verticalRad);

                double fLiftX = liftForce * liftDirX;
                double fLiftY = liftForce * liftDirY;
                double fLiftZ = liftForce * liftDirZ;

                // Accelerations (F/m)
                double ax = (fDragX + fLiftX) / BallMassKg;
                double ay = -Gravity + ((fDragY + fLiftY) / BallMassKg);
                double az = (fDragZ + fLiftZ) / BallMassKg;

                // Integrations
                vx += ax * dt;
                vy += ay * dt;
                vz += az * dt;

                px += vx * dt;
                py += vy * dt;
                pz += vz * dt;

                if (py > peakHeightMeters)
                {
                    peakHeightMeters = py;
                }

                // Sample points for visualization
                if (stepCounter % sampleRate == 0)
                {
                    result.TrajectoryPoints.Add(new Vector3D(
                        px * MetersToYards,
                        py * MetersToFeet,
                        pz * MetersToYards
                    ));
                }

                // Check Carry Landing Condition
                if (py <= 0.0 && time > 0.2) // Ground contact
                {
                    hitGround = true;

                    // Interpolate Landing Point precisely
                    double fraction = -py / (vy * dt);
                    pz -= vz * dt * fraction;
                    px -= vx * dt * fraction;

                    result.CarryDistanceYards = pz * MetersToYards;
                    result.CarryOfflineYards = px * MetersToYards;
                    result.LandingAngleDegrees = Math.Atan2(-vy, Math.Sqrt(vx * vx + vz * vz)) * (180.0 / Math.PI);
                    result.FlightTimeSeconds = time;
                }
            }

            result.ApexHeightFeet = peakHeightMeters * MetersToFeet;

            // --- GROUND BOUNCE & ROLLOUT SIMULATION ---
            SimulateRollout(px, pz, vx, vy, vz, currentSpinRpm, shot.Ball.SpinAxis, result);

            return result;
        }

        #region Aerodynamic Coefficients

        private double GetDragCoefficient(double velocityMps, double spinRatio)
        {
            double speedMph = velocityMps * MpsToMph;

            // TrackMan Drag Model Interpolation curve
            double baseCd = 0.22;
            if (speedMph > 140) baseCd = 0.21;
            else if (speedMph < 50) baseCd = 0.32;
            else baseCd = 0.32 - ((speedMph - 50.0) / 90.0) * 0.11;

            return baseCd + (0.08 * spinRatio);
        }

        private double GetLiftCoefficient(double spinRatio)
        {
            // Magnus Lift Coefficient curve based on non-dimensional spin ratio
            if (spinRatio <= 0.0) return 0.0;
            return 0.12 + 0.32 * Math.Pow(spinRatio, 0.8);
        }

        #endregion

        #region Bounce and Roll Mechanics

        private void SimulateRollout(double landingX, double landingZ, double vx, double vy, double vz, double finalSpinRpm, double spinAxis, GolfTrajectoryResult result)
        {
            // CoE Friction Coefficients for Standard Fairway
            double eNormal = 0.38;  // Vertical restitution
            double eTangent = 0.55; // Horizontal friction dissipation

            double rollVx = vx * eTangent;
            double rollVz = vz * eTangent;

            // Apply spin axis influence to initial bounce side kick
            double spinAxisRad = spinAxis * (Math.PI / 180.0);
            rollVx += Math.Sin(spinAxisRad) * (finalSpinRpm / 3000.0);

            double currentX = landingX;
            double currentZ = landingZ;

            // Ground friction deceleration loop
            double dt = 0.05;
            double muFriction = 0.35; // Fairway sliding friction
            double gMps = Gravity;

            while (Math.Sqrt(rollVx * rollVx + rollVz * rollVz) > 0.1)
            {
                double speed = Math.Sqrt(rollVx * rollVx + rollVz * rollVz);
                double decel = muFriction * gMps * dt;

                if (decel >= speed)
                {
                    break;
                }

                rollVx -= (rollVx / speed) * decel;
                rollVz -= (rollVz / speed) * decel;

                currentX += rollVx * dt;
                currentZ += rollVz * dt;
            }

            result.TotalDistanceYards = currentZ * MetersToYards;
            result.TotalOfflineYards = currentX * MetersToYards;
            result.RolloutDistanceYards = result.TotalDistanceYards - result.CarryDistanceYards;

            // Final trajectory endpoint
            result.TrajectoryPoints.Add(new Vector3D(
                result.TotalOfflineYards,
                0.0,
                result.TotalDistanceYards
            ));
        }

        #endregion

        #region Shot Classification

        /// <summary>
        /// Classifies the shot based on Horizontal Launch Angle (HLA) and Spin Axis tilt.
        /// </summary>
        public static ShotShape DetermineShotType(double hla, double spinAxis, double clubPath)
        {
            const double threshold = 1.5; // Degree tolerance for straight launch or axis

            bool isPush = hla > threshold;
            bool isPull = hla < -threshold;
            bool isStraightLaunch = !isPush && !isPull;

            bool isRightSpin = spinAxis > threshold;  // Slice / Fade curvature
            bool isLeftSpin = spinAxis < -threshold; // Hook / Draw curvature
            bool isStraightAxis = !isRightSpin && !isLeftSpin;

            if (isStraightLaunch && isStraightAxis) return ShotShape.Straight;

            if (isStraightLaunch)
            {
                if (isLeftSpin) return ShotShape.Draw;
                if (isRightSpin) return ShotShape.Fade;
            }

            if (isPush)
            {
                if (isStraightAxis) return ShotShape.Push;
                if (isLeftSpin) return ShotShape.PushDraw;
                if (isRightSpin) return ShotShape.PushSlice;
            }

            if (isPull)
            {
                if (isStraightAxis) return ShotShape.Pull;
                if (isLeftSpin) return ShotShape.PullHook;
                if (isRightSpin) return ShotShape.PullFade;
            }

            if (isRightSpin) return ShotShape.Slice;
            if (isLeftSpin) return ShotShape.Hook;

            return ShotShape.Straight;
        }

        #endregion

        public ShotRecord CalculateTrajectory(RawLaunchData raw)
        {
            ShotRecord record = new ShotRecord { ShotShape = raw.SelectedShape, LaunchAngle = raw.LaunchAngleDeg };
            double carry = 250.0; double roll = 15.0; double dev = 0.0; double apex = 30.0;

            switch (raw.SelectedShape)
            {
                case ShotShape.Straight: carry = 265; roll = 15; dev = 0; apex = 32; break;
                case ShotShape.Draw: carry = 270; roll = 18; dev = -14; apex = 29; break;
                case ShotShape.Fade: carry = 258; roll = 12; dev = 12; apex = 34; break;
                case ShotShape.Pull: carry = 260; roll = 14; dev = -22; apex = 31; break;
                case ShotShape.Push: carry = 260; roll = 14; dev = 22; apex = 31; break;
                case ShotShape.Hook: carry = 245; roll = 25; dev = -42; apex = 21; break;
                case ShotShape.Slice: carry = 230; roll = 8; dev = 45; apex = 38; break;
                case ShotShape.PullHook: carry = 250; roll = 22; dev = -58; apex = 24; break;
                case ShotShape.PushSlice: carry = 235; roll = 7; dev = 62; apex = 37; break;
            }

            record.TotalCarryYards = carry;
            record.TotalRolloutYards = roll;
            record.TotalYards = carry + roll;
            record.ApexFeet = apex;
            record.LateralDeviation = dev;

            Generate3DTrajectoryPoints(record, dev);
            return record;
        }

        private void Generate3DTrajectoryPoints(ShotRecord shot, double finalX)
        {
            int steps = 28;
            double startX = 0;

            // Handle pure linear offset deviations for Pull and Push styles prior to flight curvature integration
            if (shot.ShotShape == ShotShape.Pull || shot.ShotShape == ShotShape.PullHook) startX = -10;
            if (shot.ShotShape == ShotShape.Push || shot.ShotShape == ShotShape.PushSlice) startX = 10;

            for (int i = 0; i <= steps; i++)
            {
                double pct = (double)i / steps;
                double y = shot.TotalCarryYards * pct;
                double z = Math.Sin(pct * Math.PI) * shot.ApexFeet;

                // Exponential curve interpolation logic loops
                double x = startX + (finalX - startX) * Math.Pow(pct, 2);
                shot.CarryYards.Add(new Vector3D { X = x, Y = y, Z = z });
            }

            // Map rollout
            shot.RolloutYards.Add(new Vector3D { X = finalX, Y = shot.TotalCarryYards + (shot.TotalRolloutYards * 0.5), Z = 0 });
            shot.RolloutYards.Add(new Vector3D { X = finalX + (finalX * 0.01), Y = shot.TotalYards, Z = 0 });
        }
    }
}
