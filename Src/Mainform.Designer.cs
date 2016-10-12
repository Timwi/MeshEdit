namespace MeshEdit
{
    partial class Mainform
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
            this.mainPanel = new RT.Util.Controls.DoubleBufferedPanel();
            this.SuspendLayout();
            // 
            // mainPanel
            // 
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 0);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(784, 561);
            this.mainPanel.TabIndex = 0;
            this.mainPanel.PaintBuffer += new System.Windows.Forms.PaintEventHandler(this.paint);
            this.mainPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.mouseDown);
            this.mainPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.mouseMove);
            this.mainPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.mouseUp);
            // 
            // Mainform
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.mainPanel);
            this.Name = "Mainform";
            this.Text = "Mesh Edit";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.keyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.keyUp);
            this.Resize += new System.EventHandler(this.resize);
            this.ResumeLayout(false);

        }

        #endregion

        private RT.Util.Controls.DoubleBufferedPanel mainPanel;

    }
}

