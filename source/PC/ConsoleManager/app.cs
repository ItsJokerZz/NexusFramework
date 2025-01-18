using OrbisControlAPI;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static OrbisControlAPI.OCAPI;

namespace CM
{
    public partial class app : Form
    {
        private readonly OCAPI api = new OCAPI();
        private TreeNode selectedNode;

        public app()
        {
            InitializeComponent();

            details_p.Dock = DockStyle.Fill;

            details_b.Click += (s, e) => ChangePage(s, details_p);
            system_b.Click += (s, e) => ChangePage(s, system_p);
            memory_b.Click += (s, e) => ChangePage(s, memory_p);
            settings_b.Click += (s, e) => ChangePage(s, memory_p);

            radioButton4.CheckedChanged += (s, e) => api.Beep(BuzzType.Stop);

            label9.Text = $"OCAPI Version: {api.Version}";
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            ImageList imageList = new ImageList
            {
                ImageSize = new Size(24, 24)
            };

            imageList.Images.Add("consoleIcon", Properties.Resources.Console);

            consoles_tv.ImageList = imageList;

            string text1 = consoleName_tb.Text;
            string text2 = consoleIP_tb.Text;

            bool nodeExists = consoles_tv.Nodes
                .OfType<TreeNode>().Any(node =>
                node.Text.Contains(text1)
                || node.Text.Contains(text2));

            if (!nodeExists)
            {
                TreeNode newNode = new TreeNode($"{text1} : {text2}")
                {
                    ImageKey = "consoleIcon"
                };
                consoles_tv.Nodes.Add(newNode);
            }
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

        private void connect_Click(object sender, EventArgs e)
        {
            if (selectedNode != null) ConnectToConsole(GetConsoleIP(selectedNode));
        }

        private void consoles_tv_NodeMouseHover(object sender, TreeNodeMouseHoverEventArgs e) => selectedNode = e.Node;

        private void remove_Click(object sender, EventArgs e)
        {
            if (selectedNode == null) return;
            DisconnectFromConsole(); selectedNode.Remove();
        }

        private void consoles_tv_NodeMouseDoubleClickAsync(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                selectedNode = e.Node;
                string consoleName = GetConsoleIP(selectedNode);

                if (!api.Connected)
                    ConnectToConsole(consoleName);
                else if (api.IPAddress == consoleName
                    && api.Connected) DisconnectFromConsole();
            }
        }

        private string GetConsoleIP(TreeNode node)
        {
            return node.Text.Substring(node.Text.IndexOf(" : ") + 3);
        }

        private void ConnectToConsole(string consoleIP)
        {
            api.Connect(consoleIP);

            if (api.Connected)
            {
                api.Notify(222, "[OCAPI] Console Manager: Connected Successfully!");

                Invoke((MethodInvoker)delegate
                {
                    active_l.Text = $"Active Console: {GetConsolePrefix(selectedNode)}";
                    label6.Text = $"Firmware: {api.Firmware}";
                    label7.Text = $"CPU Temperature: {api.Temperature(Temp.getCPU)} C";
                    label15.Text = $"SOC Temperature: {api.Temperature(Temp.getSOC)} C";
                    label12.Text = $"Console Type: {api.SystemType}";
                    label10.Text = $"SPRX Version: {api.SprxVersion}";
                    label11.Text = $"Status: Connected";
                    sysInfo_t.Enabled = true;
                });

                api.Beep(BuzzType.Single);
            }
        }

        private void DisconnectFromConsole(bool unloading = false)
        {
            Invoke((MethodInvoker)delegate
            {
                active_l.Text = "Active Console: NONE";
                label6.Text = "Firmware: ?.??";
                label7.Text = $"CPU Temperature: ?? C";
                label15.Text = $"SOC Temperature: ?? C";
                label12.Text = "Console Type: ???";
                label10.Text = "SPRX Version: ?.??";
                label11.Text = "Status: Not Ready";
                sysInfo_t.Enabled = false;
            });


            if (!unloading)
            {
                api.Beep(BuzzType.Double);
                api.Disconnect();
            } else api.Beep(BuzzType.Triple);
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
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string message = textBox3.Text;
            message = message.Replace(@"\n", Environment.NewLine);

            api.Notify(int.Parse(textBox4.Text), message);
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e) => DisconnectFromConsole();

        private void ChangePage(object sender, Panel panelToShow)
        {
            system_p.Dock = DockStyle.None;
            details_p.Dock = DockStyle.None;
            memory_p.Dock = DockStyle.None;

            details_b.BackColor = Color.FromArgb(40, 40, 40);
            system_b.BackColor = Color.FromArgb(40, 40, 40);
            memory_b.BackColor = Color.FromArgb(40, 40, 40);
            settings_b.BackColor = Color.FromArgb(40, 40, 40);

            Button clickedButton = sender as Button;
            if (clickedButton != null)
                clickedButton.BackColor = Color.FromArgb(100, 100, 100);

            panelToShow.Dock = DockStyle.Fill;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!api.Connected) return;

            Invoke((MethodInvoker)delegate
            {
                label7.Text = $"CPU Temperature: {api.Temperature(Temp.getCPU)} C";
                label15.Text = $"SOC Temperature: {api.Temperature(Temp.getSOC)} C";
            });
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked) api.Beep(BuzzType.Single);
            if (radioButton2.Checked) api.Beep(BuzzType.Double);
            if (radioButton3.Checked) api.Beep(BuzzType.Triple);
            if (radioButton4.Checked) api.Beep(BuzzType.Continuous);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            api.SetTempThreshold((int)numericUpDown1.Value);
        }

        private void unloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            api.Unload(); DisconnectFromConsole(true);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            api.InjectPayload(GetConsoleIP(selectedNode));
        }

        private void button9_Click(object sender, EventArgs e)
        {
            int procHandle = api.LoadModule(textBox8.Text, textBox7.Text);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (int.TryParse(textBox10.Text, out int moduleId))
                api.UnloadModule(textBox9.Text, moduleId);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            var subnets = new[] { "192.168.137" };
            var consoles = api.FindConsoles(subnets, 243, 243);

            if (consoles.Count() != 0)
            {
                Console.WriteLine("Found console(s):");
                foreach (var console in consoles)
                    Console.WriteLine(console);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox12.Text += $"{textBox11.Text}:" + Environment.NewLine;
            textBox12.Text += api.TEST(textBox11.Text) + Environment.NewLine;
            textBox12.Text += Environment.NewLine;
        }
    }
}
