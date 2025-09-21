namespace ClickyKeys
{
    partial class GlassPanel
    {
        /// <summary> 
        /// Wymagana zmienna projektanta.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Wyczyść wszystkie używane zasoby.
        /// </summary>
        /// <param name="disposing">prawda, jeżeli zarządzane zasoby powinny zostać zlikwidowane; Fałsz w przeciwnym wypadku.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Kod wygenerowany przez Projektanta składników

        /// <summary> 
        /// Metoda wymagana do obsługi projektanta — nie należy modyfikować 
        /// jej zawartości w edytorze kodu.
        /// </summary>
        private void InitializeComponent()
        {
            lblKey = new Label();
            lblValue = new Label();
            panel = new Panel();
            SuspendLayout();
            // 
            // lblKey
            // 
            lblKey.Anchor = AnchorStyles.None;
            lblKey.Font = new Font("Segoe UI", 14F);
            lblKey.Location = new Point(30, 20);
            lblKey.Name = "lblKey";
            lblKey.Size = new Size(140, 40);
            lblKey.TabIndex = 0;
            lblKey.Text = "lblKey";
            lblKey.TextAlign = ContentAlignment.MiddleCenter;
            lblKey.Click += GlassPanel_Click;
            // 
            // lblValue
            // 
            lblValue.Anchor = AnchorStyles.Top;
            lblValue.Font = new Font("Segoe UI", 14F);
            lblValue.ForeColor = Color.Black;
            lblValue.Location = new Point(30, 60);
            lblValue.Name = "lblValue";
            lblValue.Size = new Size(140, 40);
            lblValue.TabIndex = 1;
            lblValue.Text = "lblValue";
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.Click += GlassPanel_Click;
            // 
            // panel
            // 
            panel.Location = new Point(0, 0);
            panel.Margin = new Padding(0);
            panel.Name = "panel";
            panel.Size = new Size(40, 40);
            panel.TabIndex = 2;
            panel.Visible = false;
            // 
            // GlassPanel
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Controls.Add(panel);
            Controls.Add(lblValue);
            Controls.Add(lblKey);
            DoubleBuffered = true;
            Margin = new Padding(0);
            MinimumSize = new Size(200, 120);
            Name = "GlassPanel";
            Size = new Size(200, 120);
            Click += GlassPanel_Click;
            ResumeLayout(false);

        }

        #endregion

        private Label lblKey;
        private Label lblValue;
        private Panel panel;
    }
}
