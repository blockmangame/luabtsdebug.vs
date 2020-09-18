using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebugAdapter
{
    public partial class LaunchForm : Form
    {
        public LaunchForm()
        {
            InitializeComponent();
        }

        public bool ShowLaunch(out string path, ref string dir)
        {
            if (dir != null)
            {
                comboBoxDir.Items.Add(dir);
                foreach (string p in Directory.GetFiles(dir, "*.exe"))
                {
                    if ((new FileInfo(p)).Length > 1024 * 1024)
                        comboBoxFile.Items.Add(p);
                }
            }

            if (comboBoxFile.SelectedIndex < 0 && comboBoxFile.Items.Count > 0)
                comboBoxFile.SelectedIndex = 0;

            if (comboBoxDir.SelectedIndex < 0 && comboBoxDir.Items.Count > 0)
                comboBoxDir.SelectedIndex = 0;

            if (ShowDialog() != DialogResult.OK)
            {
                path = null;
                return false;
            }

            dir = comboBoxDir.Text;
            path = comboBoxFile.Text;

            return true;
        }

        private void comboBoxFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxDir.Text = Path.GetDirectoryName(comboBoxFile.Text);
        }

        private void buttonBrowseFile_Click(object sender, EventArgs e)
        {
            if (comboBoxFile.Text != "")
            {
                openFileDialog.FileName = comboBoxFile.Text;
                openFileDialog.InitialDirectory = Path.GetDirectoryName(comboBoxFile.Text);
            }
            else if (comboBoxDir.Text != "")
            {
                openFileDialog.FileName = "";
                openFileDialog.InitialDirectory = comboBoxDir.Text;
            }
            else
            {
                openFileDialog.FileName = "";
                openFileDialog.InitialDirectory = "";
            }
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;
            comboBoxFile.Text = openFileDialog.FileName;
            comboBoxFile_SelectedIndexChanged(sender, e);
        }

        private void buttonBrowseDir_Click(object sender, EventArgs e)
        {
            if (comboBoxDir.Text != "")
                folderBrowserDialog.SelectedPath = comboBoxDir.Text;
            else if (comboBoxFile.Text != "")
                folderBrowserDialog.SelectedPath = Path.GetDirectoryName(comboBoxFile.Text);
            else
                folderBrowserDialog.SelectedPath = "";
            if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                return;
            comboBoxDir.Text = folderBrowserDialog.SelectedPath;
        }
    }
}
