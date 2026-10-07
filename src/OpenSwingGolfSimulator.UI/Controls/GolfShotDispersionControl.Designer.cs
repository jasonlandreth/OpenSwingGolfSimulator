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
    /// Designer-generated portion of <see cref="GolfShotDispersionControl"/>. This partial class
    /// declares the components that back the control (a single animation timer) and wires them up
    /// in <see cref="InitializeComponent"/>. Hand-written logic, painting, and the public API for
    /// the control live in GolfShotDispersionControl.cs, not in this file.
    /// </summary>
    partial class GolfShotDispersionControl
    {
        /// <summary>
        /// Required designer variable that tracks any components (such as the animation timer)
        /// that need to be disposed of when the control is torn down.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// The timer that drives every animated element on the control: the growing ground-track
        /// line for each shot, the hold-then-fade of each line and its lateral-offset label, and
        /// the hold-then-fade of the dispersion ellipse. A single shared timer keeps every shot's
        /// animation state advancing in lockstep, even when several shots are mid-fade at once.
        /// Declared here because it is a designer-managed component; the logic that responds to
        /// its Tick event lives in the main partial class.
        /// </summary>
        private System.Windows.Forms.Timer AnimationRefreshTimer;

        /// <summary>
        /// The right-click context menu shown when the user right-clicks directly on a landing
        /// dot, offering the option to delete that one dot. Stays hidden (via its Opening event)
        /// when a right-click does not land on any dot. Declared here because it is a designer-
        /// managed component; the logic that decides whether to show it and what happens when its
        /// item is clicked lives in the main partial class.
        /// </summary>
        private System.Windows.Forms.ContextMenuStrip DotContextMenuStrip;

        /// <summary>
        /// The single "Delete Dot" item on <see cref="DotContextMenuStrip"/>, which removes the
        /// right-clicked landing dot when clicked.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem DeleteDotMenuItem;

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
        /// (double buffering and resize-redraw) that keep the animated line, dots, and ellipse
        /// smooth and free of flicker. Following standard WinForms designer conventions, this
        /// method should not be edited by hand beyond what is generated here, since a visual
        /// designer could regenerate it.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            AnimationRefreshTimer = new System.Windows.Forms.Timer(components);
            DotContextMenuStrip = new ContextMenuStrip(components);
            DeleteDotMenuItem = new ToolStripMenuItem();
            DotContextMenuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // AnimationRefreshTimer
            // 
            AnimationRefreshTimer.Interval = 16;
            AnimationRefreshTimer.Tick += AnimationRefreshTimer_Tick;
            // 
            // DotContextMenuStrip
            // 
            DotContextMenuStrip.Items.AddRange(new ToolStripItem[] { DeleteDotMenuItem });
            DotContextMenuStrip.Name = "DotContextMenuStrip";
            DotContextMenuStrip.Size = new Size(130, 26);
            DotContextMenuStrip.Opening += DotContextMenuStrip_Opening;
            // 
            // DeleteDotMenuItem
            // 
            DeleteDotMenuItem.Name = "DeleteDotMenuItem";
            DeleteDotMenuItem.Size = new Size(129, 22);
            DeleteDotMenuItem.Text = "Delete Dot";
            DeleteDotMenuItem.Click += DeleteDotMenuItem_Click;
            // 
            // GolfShotDispersionControl
            // 
            BackColor = Color.Black;
            ContextMenuStrip = DotContextMenuStrip;
            DoubleBuffered = true;
            Name = "GolfShotDispersionControl";
            Size = new Size(1525, 262);
            MouseDown += GolfShotDispersionControl_MouseDown;
            DotContextMenuStrip.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
