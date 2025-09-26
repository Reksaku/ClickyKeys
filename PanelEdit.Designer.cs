namespace ClickyKeys
{
    partial class PanelEdit
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
            btnInput = new Button();
            textDescription = new TextBox();
            btnSave = new Button();
            btnClose = new Button();
            SuspendLayout();
            // 
            // btnInput
            // 
            btnInput.Anchor = AnchorStyles.None;
            btnInput.Font = new Font("Segoe UI", 10F);
            btnInput.Location = new Point(25, 60);
            btnInput.Margin = new Padding(0);
            btnInput.Name = "btnInput";
            btnInput.Size = new Size(86, 40);
            btnInput.TabIndex = 1;
            btnInput.Text = "Input";
            btnInput.TextAlign = ContentAlignment.TopCenter;
            btnInput.UseVisualStyleBackColor = true;
            btnInput.MouseDown += btnInput_MouseDown;
            // 
            // textDescription
            // 
            textDescription.Font = new Font("Segoe UI", 10F);
            textDescription.Location = new Point(25, 20);
            textDescription.Name = "textDescription";
            textDescription.PlaceholderText = "Description";
            textDescription.Size = new Size(155, 34);
            textDescription.TabIndex = 3;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.Lime;
            btnSave.ForeColor = Color.Black;
            btnSave.Location = new Point(114, 70);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(30, 30);
            btnSave.TabIndex = 4;
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += btnSave_Click;
            // 
            // btnClose
            // 
            btnClose.BackColor = Color.Red;
            btnClose.ForeColor = Color.Black;
            btnClose.Location = new Point(150, 70);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(30, 30);
            btnClose.TabIndex = 4;
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += btnClose_Click;
            // 
            // PanelEdit
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Controls.Add(btnClose);
            Controls.Add(btnSave);
            Controls.Add(textDescription);
            Controls.Add(btnInput);
            Name = "PanelEdit";
            Size = new Size(200, 120);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Button btnInput;
        private TextBox textDescription;
        private Button btnSave;
        private Button btnClose;
    }
}
