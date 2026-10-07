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

namespace OpenSwingGolfSimulator.UI.Controls
{
    /// <summary>
    /// Designer-generated portion of <see cref="GolfShotTracerControl"/>. This partial class
    /// declares the components that back the control (currently a single animation timer) and
    /// wires them up in <see cref="InitializeComponent"/>. Hand-written logic, painting, and the
    /// public API for the control live in GolfShotTracerControl.cs, not in this file.
    /// </summary>
    partial class GolfShotTracerControl
    {
        /// <summary>
        /// Required designer variable that tracks any components (such as the animation timer)
        /// that need to be disposed of when the control is torn down.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// The timer that drives the shot animation. Each tick advances the animation progress
        /// fraction and triggers a repaint so the ball, tracer arc, ground shadow, and yardage
        /// callouts all advance smoothly. Declared here because it is a designer-managed
        /// component; the logic that responds to its Tick event lives in the main partial class.
        /// </summary>
        private System.Windows.Forms.Timer AnimationRefreshTimer;

        /// <summary>
        /// Cleans up any resources being used, including the animation timer, and then defers to
        /// the base <see cref="System.Windows.Forms.UserControl.Dispose(bool)"/> implementation.
        /// </summary>
        /// <param name="DisposingManagedResources">
        /// True if managed resources should be disposed in addition to unmanaged resources;
        /// false if only unmanaged resources should be released (for example, when called from a
        /// finalizer).
        /// </param>
        protected override void Dispose(bool DisposingManagedResources)
        {
            if (DisposingManagedResources && (this.components != null))
            {
                this.components.Dispose();
            }

            base.Dispose(DisposingManagedResources);
        }

        /// <summary>
        /// Initializes the control's designer-managed components. This method configures the
        /// animation timer's interval and Tick handler, and sets the base rendering properties
        /// (double buffering and resize-redraw) that keep the animated tracer smooth and free of
        /// flicker. Following standard WinForms designer conventions, this method should not be
        /// edited by hand beyond what is generated here, since a visual designer could regenerate
        /// it.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            AnimationRefreshTimer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // AnimationRefreshTimer
            // 
            AnimationRefreshTimer.Interval = 16;
            AnimationRefreshTimer.Tick += AnimationRefreshTimer_Tick;
            // 
            // GolfShotTracerControl
            // 
            BackColor = Color.Black;
            BackgroundImage = Properties.Resources.driving_range;
            BackgroundImageLayout = ImageLayout.Stretch;
            DoubleBuffered = true;
            Name = "GolfShotTracerControl";
            Size = new Size(710, 1024);
            ResumeLayout(false);
        }
    }
}