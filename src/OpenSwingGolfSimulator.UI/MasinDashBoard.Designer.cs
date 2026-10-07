namespace OpenSwingGolfSimulator.UI
{
    partial class MasinDashBoard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Close = new Button();
            SuspendLayout();
            // 
            // Close
            // 
            Close.Location = new Point(572, 371);
            Close.Name = "Close";
            Close.Size = new Size(221, 67);
            Close.TabIndex = 0;
            Close.Text = "button1";
            Close.UseVisualStyleBackColor = true;
            Close.Click += Close_Click;
            // 
            // MasinDashBoard
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(Close);
            Name = "MasinDashBoard";
            Text = "MasinDashBoard";
            ResumeLayout(false);
        }

        #endregion

        private Button Close;
    }
}