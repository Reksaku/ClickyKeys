namespace ClickyKeys
{
    partial class OverlayForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OverlayForm));
            toolStrip = new ToolStrip();
            toolStripSettings = new ToolStripButton();
            toolStripHideTb = new ToolStripButton();
            toolStripReset = new ToolStripButton();
            _grid = new TableLayoutPanel();
            toolStrip.SuspendLayout();
            SuspendLayout();
            // 
            // toolStrip
            // 
            toolStrip.ImageScalingSize = new Size(24, 24);
            toolStrip.Items.AddRange(new ToolStripItem[] { toolStripSettings, toolStripHideTb, toolStripReset });
            resources.ApplyResources(toolStrip, "toolStrip");
            toolStrip.Name = "toolStrip";
            // 
            // toolStripSettings
            // 
            toolStripSettings.DisplayStyle = ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(toolStripSettings, "toolStripSettings");
            toolStripSettings.Name = "toolStripSettings";
            toolStripSettings.Click += Settings_Click;
            // 
            // toolStripHideTb
            // 
            toolStripHideTb.DisplayStyle = ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(toolStripHideTb, "toolStripHideTb");
            toolStripHideTb.Name = "toolStripHideTb";
            toolStripHideTb.Click += toolStripHideTb_Click;
            // 
            // toolStripReset
            // 
            toolStripReset.DisplayStyle = ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(toolStripReset, "toolStripReset");
            toolStripReset.Name = "toolStripReset";
            toolStripReset.Click += toolStripReset_Click;
            // 
            // _grid
            // 
            resources.ApplyResources(_grid, "_grid");
            _grid.Name = "_grid";
            // 
            // OverlayForm
            // 
            AutoScaleMode = AutoScaleMode.None;
            resources.ApplyResources(this, "$this");
            Controls.Add(_grid);
            Controls.Add(toolStrip);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "OverlayForm";
            KeyDown += OverlayForm_KeyDown;
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolStripSettings;
        private System.Windows.Forms.TableLayoutPanel _grid;
        private ToolStripButton toolStripReset;
        private ToolStripButton toolStripHideTb;
    }
}