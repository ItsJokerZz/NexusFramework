using OrbisControlAPI;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CM
{
    public partial class app : Form
    {
        readonly OCAPI api = new OCAPI();
        TreeNode selectedNode;

        string IPAddress;

        public app()
        {
            InitializeComponent();
            panel2.Dock = DockStyle.Fill;

            details_b.Click += (s, e) => ChangePage(s, panel2);
            system_b.Click += (s, e) => ChangePage(s, panel1);
            memory_b.Click += (s, e) => ChangePage(s, panel4);

            radioButton4.CheckedChanged += async (s, e) => await api.Beep(OCAPI.BeepType.Stop);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string text1 = toolStripTextBox1.Text;
            string text2 = toolStripTextBox2.Text;
            bool nodeExists = consoles_tv.Nodes
                .OfType<TreeNode>().Any(node =>
                node.Text.Contains(text1)
                || node.Text.Contains(text2));

            if (!nodeExists)
                consoles_tv.Nodes.Add(new TreeNode($"{text1} : {text2}"));
        }

        private void consoles_tv_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                selectedNode = e.Node;
                toolStripTextBox3.Text = GetConsolePrefix(selectedNode);
                contextMenuStrip1.Show(e.Node.TreeView, e.Location);
            }
        }

        private async void connect_Click(object sender, EventArgs e)
        {
            if (selectedNode != null)
            {
                string consoleName = GetConsoleIP(selectedNode);
                await ConnectToConsole(consoleName);
            }
        }

        private void consoles_tv_NodeMouseHover(object sender, TreeNodeMouseHoverEventArgs e)
        {
            selectedNode = e.Node;
        }

        private void remove_Click(object sender, EventArgs e)
        {
            if (selectedNode != null)
            {
                DisconnectFromConsole();
                selectedNode.Remove();
            }
        }

        private async void consoles_tv_NodeMouseDoubleClickAsync(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                selectedNode = e.Node;
                string consoleName = GetConsoleIP(selectedNode);

                if (!api.Connected)
                    await ConnectToConsole(consoleName);
                else if (api.IPAddress == consoleName
                    && api.Connected) DisconnectFromConsole();
            }
        }

        private string GetConsoleIP(TreeNode node)
        {
            return node.Text.Substring(node.Text.IndexOf(" : ") + 3);
        }

        private async Task ConnectToConsole(string consoleName)
        {
            await api.Connect(consoleName);

            if (api.Connected)
            {
                await api.Notify(222, "[OCAPI] Console Manager: Connected Successfully!");

                active_t.Text = $"Active Console: {GetConsolePrefix(selectedNode)}";

                label6.Text = $"Firmware: {api.Firmware}";
                label7.Text = $"Temperature: {api.Temperature} C";
                label12.Text = $"Console Type: {api.SystemType}";

                label10.Text = $"PS4 Version: {api.Version}";
                label9.Text = $"API Version: {api.PS4Version}";

                label11.Text = $"Status: Connected";

                timer1.Enabled = true;
            }
        }

        private async void DisconnectFromConsole(bool unloading = false)
        {
            active_t.Text = "Active Console: NONE";

            label6.Text = "Firmware: ?.??";
            label7.Text = $"Temperature: ?? C";
            label12.Text = "Console Type: ???";

            label10.Text = "PS4 Version: ?.??";
            label9.Text = "API Version: ?.??";
            label11.Text = "Status: Not Ready";

            timer1.Enabled = false;

            if (unloading) return;
            await api.Disconnect();
        }

        private string GetConsolePrefix(TreeNode node)
        {
            int separatorIndex = node.Text.IndexOf(" : ");
            return separatorIndex >= 0 ? node.Text.Substring(0, separatorIndex) : node.Text;
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedNode != null)
            {
                string newName = toolStripTextBox3.Text;
                int separatorIndex = selectedNode.Text.IndexOf(" : ");

                if (separatorIndex >= 0)
                {
                    string currentSuffix = selectedNode.Text.Substring(separatorIndex);
                    selectedNode.Text = $"{newName}{currentSuffix}";
                }
                else
                {
                    // Handle case where there is no " : " in the text, if needed
                    selectedNode.Text = newName;
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string message = textBox3.Text;
            message = message.Replace(@"\n", Environment.NewLine);

            await api.Notify(int.Parse(textBox4.Text), message);
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e) => DisconnectFromConsole();

        private void ChangePage(object sender, Panel panelToShow)
        {
            panel1.Dock = DockStyle.None;
            panel2.Dock = DockStyle.None;
            panel4.Dock = DockStyle.None;

            details_b.BackColor = Color.FromArgb(40, 40, 40);
            system_b.BackColor = Color.FromArgb(40, 40, 40);
            memory_b.BackColor = Color.FromArgb(40, 40, 40);

            Button clickedButton = sender as Button;
            if (clickedButton != null)
                clickedButton.BackColor =
            Color.FromArgb(100, 100, 100);

            panelToShow.Dock = DockStyle.Fill;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (api.Connected) label7.Text = $"Temperature: {api.Temperature} C";
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked) await api.Beep(OCAPI.BeepType.Single);
            if (radioButton2.Checked) await api.Beep(OCAPI.BeepType.Double);
            if (radioButton3.Checked) await api.Beep(OCAPI.BeepType.Triple);
            if (radioButton4.Checked) await api.Beep(OCAPI.BeepType.Continuous);
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            await api.SetTempThreshold((int)numericUpDown1.Value);
        }

        private async void unloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await api.Unload(); DisconnectFromConsole(true);
        }

        private async void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            await api.InjectPayload(GetConsoleIP(selectedNode));
        }
    }
}