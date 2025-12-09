namespace YoloDetectorApp
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnLoadModel;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblFPS;
        private System.Windows.Forms.Label lblDetectionsCount;
        private System.Windows.Forms.GroupBox groupBoxDetections;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnLoadModel = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblFPS = new System.Windows.Forms.Label();
            this.lblDetectionsCount = new System.Windows.Forms.Label();
            this.groupBoxDetections = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.groupBoxDetections.SuspendLayout();
            this.SuspendLayout();

            // pictureBox
            this.pictureBox.BackColor = System.Drawing.Color.Black;
            this.pictureBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pictureBox.Location = new System.Drawing.Point(12, 12);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(640, 480);
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;

            // btnStart
            this.btnStart.BackColor = System.Drawing.Color.FromArgb(76, 175, 80);
            this.btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStart.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.ForeColor = System.Drawing.Color.White;
            this.btnStart.Location = new System.Drawing.Point(12, 510);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(120, 40);
            this.btnStart.TabIndex = 1;
            this.btnStart.Text = "СТАРТ";
            this.btnStart.UseVisualStyleBackColor = false;

            // btnStop
            this.btnStop.BackColor = System.Drawing.Color.FromArgb(244, 67, 54);
            this.btnStop.Enabled = false;
            this.btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.btnStop.ForeColor = System.Drawing.Color.White;
            this.btnStop.Location = new System.Drawing.Point(138, 510);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 40);
            this.btnStop.TabIndex = 2;
            this.btnStop.Text = "СТОП";
            this.btnStop.UseVisualStyleBackColor = false;

            // btnLoadModel
            this.btnLoadModel.BackColor = System.Drawing.Color.FromArgb(33, 150, 243);
            this.btnLoadModel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoadModel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.btnLoadModel.ForeColor = System.Drawing.Color.White;
            this.btnLoadModel.Location = new System.Drawing.Point(12, 560);
            this.btnLoadModel.Name = "btnLoadModel";
            this.btnLoadModel.Size = new System.Drawing.Size(246, 40);
            this.btnLoadModel.TabIndex = 3;
            this.btnLoadModel.Text = "ЗАГРУЗИТЬ МОДЕЛЬ";
            this.btnLoadModel.UseVisualStyleBackColor = false;

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.Red;
            this.lblStatus.Location = new System.Drawing.Point(264, 520);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(172, 20);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "Статус: ВЫКЛЮЧЕНО";

            // lblFPS
            this.lblFPS.AutoSize = true;
            this.lblFPS.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblFPS.Location = new System.Drawing.Point(264, 550);
            this.lblFPS.Name = "lblFPS";
            this.lblFPS.Size = new System.Drawing.Size(52, 20);
            this.lblFPS.TabIndex = 5;
            this.lblFPS.Text = "FPS: 0";

            // groupBoxDetections
            this.groupBoxDetections.Controls.Add(this.lblDetectionsCount);
            this.groupBoxDetections.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxDetections.Location = new System.Drawing.Point(668, 12);
            this.groupBoxDetections.Name = "groupBoxDetections";
            this.groupBoxDetections.Size = new System.Drawing.Size(280, 180);
            this.groupBoxDetections.TabIndex = 6;
            this.groupBoxDetections.TabStop = false;
            this.groupBoxDetections.Text = "ОБНАРУЖЕНО";

            // lblDetectionsCount
            this.lblDetectionsCount.AutoSize = true;
            this.lblDetectionsCount.Font = new System.Drawing.Font("Arial", 24F, System.Drawing.FontStyle.Bold);
            this.lblDetectionsCount.ForeColor = System.Drawing.Color.FromArgb(33, 150, 243);
            this.lblDetectionsCount.Location = new System.Drawing.Point(100, 70);
            this.lblDetectionsCount.Name = "lblDetectionsCount";
            this.lblDetectionsCount.Size = new System.Drawing.Size(42, 46);
            this.lblDetectionsCount.TabIndex = 0;
            this.lblDetectionsCount.Text = "0";

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(960, 618);
            this.Controls.Add(this.groupBoxDetections);
            this.Controls.Add(this.lblFPS);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnLoadModel);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.pictureBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "YOLO Object Detection - Веб-камера";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.groupBoxDetections.ResumeLayout(false);
            this.groupBoxDetections.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}