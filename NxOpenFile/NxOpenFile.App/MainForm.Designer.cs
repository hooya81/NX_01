namespace NxOpenFile.App
{
    partial class MainForm
    {
        /// <summary>필수 디자이너 변수입니다.</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>사용 중인 모든 리소스를 정리합니다.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        private void InitializeComponent()
        {
            this.btnOpen = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // btnOpen
            //
            this.btnOpen.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpen.Font = new System.Drawing.Font("맑은 고딕", 11F);
            this.btnOpen.Location = new System.Drawing.Point(16, 16);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(392, 52);
            this.btnOpen.TabIndex = 0;
            this.btnOpen.Text = "NX에서 파일 열기...";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            //
            // lblStatus
            //
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.lblStatus.Location = new System.Drawing.Point(16, 80);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(392, 44);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "NX 연결 확인 중...";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(424, 136);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnOpen);
            this.MinimumSize = new System.Drawing.Size(360, 175);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NX 파일 열기";
            this.TopMost = true;
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Label lblStatus;
    }
}
