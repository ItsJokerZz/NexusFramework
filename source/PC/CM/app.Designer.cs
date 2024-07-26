namespace CM
{
    partial class app
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(app));
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripTextBox2 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.consoles_tv = new System.Windows.Forms.TreeView();
            this.consoles_p = new System.Windows.Forms.Panel();
            this.consoles_t = new System.Windows.Forms.Label();
            this.details_b = new System.Windows.Forms.Button();
            this.system_b = new System.Windows.Forms.Button();
            this.memory_b = new System.Windows.Forms.Button();
            this.pages = new System.Windows.Forms.Panel();
            this.bottom_p = new System.Windows.Forms.Panel();
            this.active_t = new System.Windows.Forms.Label();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.connect = new System.Windows.Forms.ToolStripMenuItem();
            this.remove = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripTextBox3 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStrip.SuspendLayout();
            this.consoles_p.SuspendLayout();
            this.bottom_p.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox1,
            this.toolStripSeparator1,
            this.toolStripTextBox2,
            this.toolStripSeparator2,
            this.toolStripButton1});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(800, 25);
            this.toolStrip.TabIndex = 0;
            this.toolStrip.Text = "toolStrip1";
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.toolStripTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.toolStripTextBox1.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripTextBox1.ForeColor = System.Drawing.Color.White;
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(100, 25);
            this.toolStripTextBox1.Text = "Default";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripTextBox2
            // 
            this.toolStripTextBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.toolStripTextBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.toolStripTextBox2.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripTextBox2.ForeColor = System.Drawing.Color.White;
            this.toolStripTextBox2.Name = "toolStripTextBox2";
            this.toolStripTextBox2.Size = new System.Drawing.Size(100, 25);
            this.toolStripTextBox2.Text = "192.168.137.130";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.AutoToolTip = false;
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.ForeColor = System.Drawing.Color.White;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(79, 22);
            this.toolStripButton1.Text = "Add Console";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // consoles_tv
            // 
            this.consoles_tv.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.consoles_tv.ForeColor = System.Drawing.Color.White;
            this.consoles_tv.LineColor = System.Drawing.Color.White;
            this.consoles_tv.Location = new System.Drawing.Point(12, 55);
            this.consoles_tv.Name = "consoles_tv";
            this.consoles_tv.ShowLines = false;
            this.consoles_tv.ShowPlusMinus = false;
            this.consoles_tv.ShowRootLines = false;
            this.consoles_tv.Size = new System.Drawing.Size(198, 257);
            this.consoles_tv.TabIndex = 1;
            this.consoles_tv.NodeMouseHover += new System.Windows.Forms.TreeNodeMouseHoverEventHandler(this.consoles_tv_NodeMouseHover);
            this.consoles_tv.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.consoles_tv_NodeMouseClick);
            this.consoles_tv.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.consoles_tv_NodeMouseDoubleClickAsync);
            // 
            // consoles_p
            // 
            this.consoles_p.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.consoles_p.Controls.Add(this.consoles_t);
            this.consoles_p.Location = new System.Drawing.Point(12, 37);
            this.consoles_p.Name = "consoles_p";
            this.consoles_p.Size = new System.Drawing.Size(198, 18);
            this.consoles_p.TabIndex = 2;
            // 
            // consoles_t
            // 
            this.consoles_t.AutoSize = true;
            this.consoles_t.Dock = System.Windows.Forms.DockStyle.Left;
            this.consoles_t.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.consoles_t.ForeColor = System.Drawing.Color.White;
            this.consoles_t.Location = new System.Drawing.Point(0, 0);
            this.consoles_t.Name = "consoles_t";
            this.consoles_t.Size = new System.Drawing.Size(81, 19);
            this.consoles_t.TabIndex = 3;
            this.consoles_t.Text = "Consoles";
            // 
            // details_b
            // 
            this.details_b.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.details_b.FlatAppearance.BorderSize = 0;
            this.details_b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.details_b.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.details_b.ForeColor = System.Drawing.Color.White;
            this.details_b.Location = new System.Drawing.Point(226, 37);
            this.details_b.Name = "details_b";
            this.details_b.Size = new System.Drawing.Size(75, 23);
            this.details_b.TabIndex = 3;
            this.details_b.Text = "Details";
            this.details_b.UseVisualStyleBackColor = false;
            // 
            // system_b
            // 
            this.system_b.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.system_b.FlatAppearance.BorderSize = 0;
            this.system_b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.system_b.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.system_b.ForeColor = System.Drawing.Color.White;
            this.system_b.Location = new System.Drawing.Point(301, 37);
            this.system_b.Name = "system_b";
            this.system_b.Size = new System.Drawing.Size(75, 23);
            this.system_b.TabIndex = 4;
            this.system_b.Text = "System";
            this.system_b.UseVisualStyleBackColor = false;
            // 
            // memory_b
            // 
            this.memory_b.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.memory_b.FlatAppearance.BorderSize = 0;
            this.memory_b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.memory_b.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.memory_b.ForeColor = System.Drawing.Color.White;
            this.memory_b.Location = new System.Drawing.Point(376, 37);
            this.memory_b.Name = "memory_b";
            this.memory_b.Size = new System.Drawing.Size(75, 23);
            this.memory_b.TabIndex = 5;
            this.memory_b.Text = "Memory";
            this.memory_b.UseVisualStyleBackColor = false;
            // 
            // pages
            // 
            this.pages.Location = new System.Drawing.Point(226, 66);
            this.pages.Name = "pages";
            this.pages.Size = new System.Drawing.Size(562, 246);
            this.pages.TabIndex = 6;
            // 
            // bottom_p
            // 
            this.bottom_p.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.bottom_p.Controls.Add(this.active_t);
            this.bottom_p.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom_p.Location = new System.Drawing.Point(0, 322);
            this.bottom_p.Name = "bottom_p";
            this.bottom_p.Size = new System.Drawing.Size(800, 16);
            this.bottom_p.TabIndex = 7;
            // 
            // active_t
            // 
            this.active_t.AutoSize = true;
            this.active_t.Dock = System.Windows.Forms.DockStyle.Right;
            this.active_t.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.active_t.ForeColor = System.Drawing.Color.White;
            this.active_t.Location = new System.Drawing.Point(653, 0);
            this.active_t.Name = "active_t";
            this.active_t.Size = new System.Drawing.Size(147, 15);
            this.active_t.TabIndex = 9;
            this.active_t.Text = "Active Console: NONE";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.connect,
            this.remove,
            this.renameToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 92);
            // 
            // connect
            // 
            this.connect.Name = "connect";
            this.connect.Size = new System.Drawing.Size(180, 22);
            this.connect.Text = "Connect";
            this.connect.Click += new System.EventHandler(this.connect_Click);
            // 
            // remove
            // 
            this.remove.Name = "remove";
            this.remove.Size = new System.Drawing.Size(180, 22);
            this.remove.Text = "Remove";
            this.remove.Click += new System.EventHandler(this.remove_Click);
            // 
            // renameToolStripMenuItem
            // 
            this.renameToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox3});
            this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            this.renameToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.renameToolStripMenuItem.Text = "Rename";
            this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
            // 
            // toolStripTextBox3
            // 
            this.toolStripTextBox3.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripTextBox3.Name = "toolStripTextBox3";
            this.toolStripTextBox3.Size = new System.Drawing.Size(100, 23);
            // 
            // app
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(75)))), ((int)(((byte)(75)))));
            this.ClientSize = new System.Drawing.Size(800, 338);
            this.Controls.Add(this.bottom_p);
            this.Controls.Add(this.pages);
            this.Controls.Add(this.memory_b);
            this.Controls.Add(this.system_b);
            this.Controls.Add(this.details_b);
            this.Controls.Add(this.consoles_tv);
            this.Controls.Add(this.consoles_p);
            this.Controls.Add(this.toolStrip);
            this.Name = "app";
            this.Text = "Form1";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.consoles_p.ResumeLayout(false);
            this.consoles_p.PerformLayout();
            this.bottom_p.ResumeLayout(false);
            this.bottom_p.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.TreeView consoles_tv;
        private System.Windows.Forms.Panel consoles_p;
        private System.Windows.Forms.Label consoles_t;
        private System.Windows.Forms.Button details_b;
        private System.Windows.Forms.Button system_b;
        private System.Windows.Forms.Button memory_b;
        private System.Windows.Forms.Panel pages;
        private System.Windows.Forms.Panel bottom_p;
        private System.Windows.Forms.Label active_t;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem connect;
        private System.Windows.Forms.ToolStripMenuItem remove;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox3;
    }
}

