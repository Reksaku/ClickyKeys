namespace ClickyKeys
{
    partial class SettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            btnSave = new Button();
            btnExit = new Button();
            label1 = new Label();
            tableLayoutPanel1 = new TableLayoutPanel();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            trBrColumns = new TrackBar();
            comboBox1 = new ComboBox();
            trBrRows = new TrackBar();
            btnKeyTextColor = new Button();
            btnValueTextColor = new Button();
            btnPanelsColor = new Button();
            btnBackgroundColor = new Button();
            label7 = new Label();
            label8 = new Label();
            trBrOpacity = new TrackBar();
            colDial = new ColorDialog();
            btnApply = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trBrColumns).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trBrRows).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trBrOpacity).BeginInit();
            SuspendLayout();
            // 
            // btnSave
            // 
            btnSave.Location = new Point(558, 685);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(112, 34);
            btnSave.TabIndex = 0;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnExit
            // 
            btnExit.Location = new Point(676, 685);
            btnExit.Name = "btnExit";
            btnExit.Size = new Size(112, 34);
            btnExit.TabIndex = 1;
            btnExit.Text = "Exit";
            btnExit.UseVisualStyleBackColor = true;
            btnExit.MouseClick += btnExit_MouseClick;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14F);
            label1.Location = new Point(3, 0);
            label1.Name = "label1";
            label1.Size = new Size(137, 38);
            label1.TabIndex = 2;
            label1.Text = "Language";
            label1.TextAlign = ContentAlignment.MiddleLeft;
            label1.Visible = false;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.None;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27.7061863F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72.2938156F));
            tableLayoutPanel1.Controls.Add(label1, 0, 0);
            tableLayoutPanel1.Controls.Add(label2, 0, 1);
            tableLayoutPanel1.Controls.Add(label3, 0, 2);
            tableLayoutPanel1.Controls.Add(label4, 0, 3);
            tableLayoutPanel1.Controls.Add(label5, 0, 4);
            tableLayoutPanel1.Controls.Add(label6, 0, 5);
            tableLayoutPanel1.Controls.Add(trBrColumns, 1, 2);
            tableLayoutPanel1.Controls.Add(comboBox1, 1, 0);
            tableLayoutPanel1.Controls.Add(trBrRows, 1, 1);
            tableLayoutPanel1.Controls.Add(btnKeyTextColor, 1, 3);
            tableLayoutPanel1.Controls.Add(btnValueTextColor, 1, 4);
            tableLayoutPanel1.Controls.Add(btnPanelsColor, 1, 5);
            tableLayoutPanel1.Controls.Add(btnBackgroundColor, 1, 6);
            tableLayoutPanel1.Controls.Add(label7, 0, 6);
            tableLayoutPanel1.Controls.Add(label8, 0, 7);
            tableLayoutPanel1.Controls.Add(trBrOpacity, 1, 7);
            tableLayoutPanel1.Location = new Point(12, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 10;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.Size = new Size(776, 667);
            tableLayoutPanel1.TabIndex = 4;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 14F);
            label2.Location = new Point(3, 66);
            label2.Name = "label2";
            label2.Size = new Size(81, 38);
            label2.TabIndex = 2;
            label2.Text = "Rows";
            label2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 14F);
            label3.Location = new Point(3, 132);
            label3.Name = "label3";
            label3.Size = new Size(132, 38);
            label3.TabIndex = 2;
            label3.Text = "Collumns";
            label3.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI", 14F);
            label4.Location = new Point(3, 198);
            label4.Name = "label4";
            label4.Size = new Size(156, 38);
            label4.TabIndex = 2;
            label4.Text = "Keys colors";
            label4.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 14F);
            label5.Location = new Point(3, 264);
            label5.Name = "label5";
            label5.Size = new Size(178, 38);
            label5.TabIndex = 2;
            label5.Text = "Values colors";
            label5.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 14F);
            label6.Location = new Point(3, 330);
            label6.Name = "label6";
            label6.Size = new Size(166, 38);
            label6.TabIndex = 2;
            label6.Text = "Panels color";
            label6.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // trBrColumns
            // 
            trBrColumns.Anchor = AnchorStyles.None;
            trBrColumns.Location = new Point(218, 135);
            trBrColumns.Minimum = 1;
            trBrColumns.Name = "trBrColumns";
            trBrColumns.Size = new Size(555, 60);
            trBrColumns.TabIndex = 4;
            trBrColumns.Value = 5;
            trBrColumns.ValueChanged += trBrColumns_ValueChanged;
            // 
            // comboBox1
            // 
            comboBox1.Anchor = AnchorStyles.Top;
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(307, 3);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(377, 33);
            comboBox1.TabIndex = 3;
            comboBox1.Visible = false;
            // 
            // trBrRows
            // 
            trBrRows.Anchor = AnchorStyles.None;
            trBrRows.Location = new Point(218, 69);
            trBrRows.Minimum = 1;
            trBrRows.Name = "trBrRows";
            trBrRows.Size = new Size(555, 60);
            trBrRows.TabIndex = 4;
            trBrRows.Value = 2;
            trBrRows.ValueChanged += trBrRows_ValueChanged;
            // 
            // btnKeyTextColor
            // 
            btnKeyTextColor.Anchor = AnchorStyles.Top;
            btnKeyTextColor.Location = new Point(313, 201);
            btnKeyTextColor.Name = "btnKeyTextColor";
            btnKeyTextColor.Size = new Size(365, 35);
            btnKeyTextColor.TabIndex = 5;
            btnKeyTextColor.Text = "Text";
            btnKeyTextColor.UseVisualStyleBackColor = true;
            btnKeyTextColor.Click += btnKeyTextColor_Click;
            // 
            // btnValueTextColor
            // 
            btnValueTextColor.Anchor = AnchorStyles.Top;
            btnValueTextColor.Location = new Point(313, 267);
            btnValueTextColor.Name = "btnValueTextColor";
            btnValueTextColor.Size = new Size(365, 35);
            btnValueTextColor.TabIndex = 5;
            btnValueTextColor.Text = "Text";
            btnValueTextColor.UseVisualStyleBackColor = true;
            btnValueTextColor.Click += btnValueTextColor_Click;
            // 
            // btnPanelsColor
            // 
            btnPanelsColor.Anchor = AnchorStyles.Top;
            btnPanelsColor.Location = new Point(313, 333);
            btnPanelsColor.Name = "btnPanelsColor";
            btnPanelsColor.Size = new Size(365, 35);
            btnPanelsColor.TabIndex = 5;
            btnPanelsColor.Text = "Panels";
            btnPanelsColor.UseVisualStyleBackColor = true;
            btnPanelsColor.Click += btnPanelsColor_Click;
            // 
            // btnBackgroundColor
            // 
            btnBackgroundColor.Anchor = AnchorStyles.Top;
            btnBackgroundColor.Location = new Point(313, 399);
            btnBackgroundColor.Name = "btnBackgroundColor";
            btnBackgroundColor.Size = new Size(365, 35);
            btnBackgroundColor.TabIndex = 5;
            btnBackgroundColor.Text = "Background";
            btnBackgroundColor.UseVisualStyleBackColor = true;
            btnBackgroundColor.Click += btnBackgroundColor_Click;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Segoe UI", 14F);
            label7.Location = new Point(3, 396);
            label7.Name = "label7";
            label7.Size = new Size(172, 66);
            label7.TabIndex = 2;
            label7.Text = "Background color";
            label7.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Segoe UI", 14F);
            label8.Location = new Point(3, 462);
            label8.Name = "label8";
            label8.Size = new Size(193, 38);
            label8.TabIndex = 2;
            label8.Text = "Panels opacity";
            label8.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // trBrOpacity
            // 
            trBrOpacity.Anchor = AnchorStyles.None;
            trBrOpacity.LargeChange = 1;
            trBrOpacity.Location = new Point(218, 465);
            trBrOpacity.Maximum = 255;
            trBrOpacity.Name = "trBrOpacity";
            trBrOpacity.Size = new Size(555, 60);
            trBrOpacity.TabIndex = 1;
            trBrOpacity.TickFrequency = 22;
            trBrOpacity.Value = 180;
            trBrOpacity.ValueChanged += trBrOpacity_ValueChanged;
            // 
            // btnApply
            // 
            btnApply.Location = new Point(440, 685);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(112, 34);
            btnApply.TabIndex = 0;
            btnApply.Text = "Apply";
            btnApply.UseVisualStyleBackColor = true;
            btnApply.Click += btnApply_Click;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 731);
            Controls.Add(btnExit);
            Controls.Add(btnApply);
            Controls.Add(btnSave);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "SettingsForm";
            Text = "Settings";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trBrColumns).EndInit();
            ((System.ComponentModel.ISupportInitialize)trBrRows).EndInit();
            ((System.ComponentModel.ISupportInitialize)trBrOpacity).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Button btnSave;
        private Button btnExit;
        private Label label1;
        private TableLayoutPanel tableLayoutPanel1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
        private ComboBox comboBox1;
        private TrackBar trBrRows;
        private TrackBar trBrColumns;
        private Button btnValueTextColor;
        private ColorDialog colDial;
        private Button btnKeyTextColor;
        private Button btnApply;
        private Button btnPanelsColor;
        private Button btnBackgroundColor;
        private Label label7;
        private TrackBar trBrOpacity;
        private Label label8;
    }
}