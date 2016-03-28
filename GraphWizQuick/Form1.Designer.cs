namespace GraphWizQuick
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.binFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dotFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.btnResetZoom = new System.Windows.Forms.ToolStripButton();
            this.labelZoom = new System.Windows.Forms.ToolStripLabel();
            this.panel = new GraphWizQuick.ImagePanel();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(40, 40);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(16, 5, 0, 5);
            this.menuStrip1.Size = new System.Drawing.Size(1451, 55);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.binFolderToolStripMenuItem,
            this.dotFileToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(75, 45);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // binFolderToolStripMenuItem
            // 
            this.binFolderToolStripMenuItem.Name = "binFolderToolStripMenuItem";
            this.binFolderToolStripMenuItem.Size = new System.Drawing.Size(266, 46);
            this.binFolderToolStripMenuItem.Text = "Bin Folder";
            this.binFolderToolStripMenuItem.Click += new System.EventHandler(this.binFolderToolStripMenuItem_Click);
            // 
            // dotFileToolStripMenuItem
            // 
            this.dotFileToolStripMenuItem.Name = "dotFileToolStripMenuItem";
            this.dotFileToolStripMenuItem.Size = new System.Drawing.Size(266, 46);
            this.dotFileToolStripMenuItem.Text = "Dot File";
            this.dotFileToolStripMenuItem.Click += new System.EventHandler(this.dotFileToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(266, 46);
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(40, 40);
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(98, 4);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(40, 40);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRefresh,
            this.btnResetZoom,
            this.labelZoom});
            this.toolStrip1.Location = new System.Drawing.Point(0, 55);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.toolStrip1.Size = new System.Drawing.Size(1451, 48);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnRefresh
            // 
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRefresh.Image = ((System.Drawing.Image)(resources.GetObject("btnRefresh.Image")));
            this.btnRefresh.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(120, 45);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Click += new System.EventHandler(this.Refresh_Click);
            // 
            // btnResetZoom
            // 
            this.btnResetZoom.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnResetZoom.Image = ((System.Drawing.Image)(resources.GetObject("btnResetZoom.Image")));
            this.btnResetZoom.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnResetZoom.Name = "btnResetZoom";
            this.btnResetZoom.Size = new System.Drawing.Size(181, 45);
            this.btnResetZoom.Text = "Reset Zoom";
            this.btnResetZoom.Click += new System.EventHandler(this.ResetZoom_Click);
            // 
            // labelZoom
            // 
            this.labelZoom.Name = "labelZoom";
            this.labelZoom.Size = new System.Drawing.Size(91, 45);
            this.labelZoom.Text = "100%";
            // 
            // panel
            // 
            this.panel.CanvasSize = new System.Drawing.Size(60, 40);
            this.panel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel.Image = null;
            this.panel.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            this.panel.Location = new System.Drawing.Point(0, 103);
            this.panel.Name = "panel";
            this.panel.Size = new System.Drawing.Size(1451, 741);
            this.panel.TabIndex = 2;
            this.panel.Zoom = 1F;
            this.panel.ZoomPercent = 100;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(16F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1451, 844);
            this.Controls.Add(this.panel);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem binFolderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dotFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripButton btnResetZoom;
        private System.Windows.Forms.ToolStripLabel labelZoom;
        private ImagePanel panel;
    }
}

