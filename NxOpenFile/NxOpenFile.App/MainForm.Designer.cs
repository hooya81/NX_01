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
            this.btnComponents = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lvComponents = new System.Windows.Forms.ListView();
            this.colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colLevel = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colFile = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            //
            // btnOpen
            //
            this.btnOpen.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.btnOpen.Location = new System.Drawing.Point(16, 16);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(200, 44);
            this.btnOpen.TabIndex = 0;
            this.btnOpen.Text = "NX에서 파일 열기...";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            //
            // btnComponents
            //
            this.btnComponents.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.btnComponents.Location = new System.Drawing.Point(228, 16);
            this.btnComponents.Name = "btnComponents";
            this.btnComponents.Size = new System.Drawing.Size(200, 44);
            this.btnComponents.TabIndex = 1;
            this.btnComponents.Text = "컴포넌트 목록";
            this.btnComponents.UseVisualStyleBackColor = true;
            this.btnComponents.Click += new System.EventHandler(this.btnComponents_Click);
            //
            // lblStatus
            //
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.lblStatus.Location = new System.Drawing.Point(16, 68);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(728, 40);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "NX 연결 확인 중...";
            //
            // lvComponents
            //
            this.lvComponents.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvComponents.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colLevel,
            this.colFile,
            this.colState});
            this.lvComponents.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.lvComponents.FullRowSelect = true;
            this.lvComponents.GridLines = true;
            this.lvComponents.HideSelection = false;
            this.lvComponents.Location = new System.Drawing.Point(16, 112);
            this.lvComponents.Name = "lvComponents";
            this.lvComponents.Size = new System.Drawing.Size(728, 368);
            this.lvComponents.TabIndex = 3;
            this.lvComponents.UseCompatibleStateImageBehavior = false;
            this.lvComponents.View = System.Windows.Forms.View.Details;
            //
            // colName
            //
            this.colName.Text = "컴포넌트";
            this.colName.Width = 260;
            //
            // colLevel
            //
            this.colLevel.Text = "레벨";
            this.colLevel.Width = 50;
            //
            // colFile
            //
            this.colFile.Text = "파트 파일";
            this.colFile.Width = 330;
            //
            // colState
            //
            this.colState.Text = "상태";
            this.colState.Width = 70;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 496);
            this.Controls.Add(this.lvComponents);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnComponents);
            this.Controls.Add(this.btnOpen);
            this.MinimumSize = new System.Drawing.Size(480, 320);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NX 파일 열기";
            this.TopMost = true;
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnComponents;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ListView lvComponents;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colLevel;
        private System.Windows.Forms.ColumnHeader colFile;
        private System.Windows.Forms.ColumnHeader colState;
    }
}
