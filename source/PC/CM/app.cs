using OrbisControlAPI;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CM
{
    public partial class app : Form
    {
        readonly OCAPI api = new OCAPI();
        TreeNode selectedNode;

        public app()
        {
            InitializeComponent();
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
                DisconnectConsole();
        }

        private async void consoles_tv_NodeMouseDoubleClickAsync(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                selectedNode = e.Node;
                string consoleName = GetConsoleIP(selectedNode);

                if (api.Connected())
                    selectedNode.Remove();
                else await ConnectToConsole(consoleName);
                
            }
        }

        private string GetConsoleIP(TreeNode node)
        {
            return node.Text.Substring(node.Text.IndexOf(" : ") + 3);
        }

        private async Task ConnectToConsole(string consoleName)
        {
            await api.Connect(consoleName);

            if (api.Connected())
            {
                active_t.Text = $"Active Console: {GetConsolePrefix(selectedNode)}";
                api.Notify(222, @"[OCAPI] Console Manager: Connected Successfully!");
            }
        }

        private void DisconnectConsole()
        {
            selectedNode.Remove();
            if (api.Connected())
            {
                api.Disconnect(); active_t.Text = "Active Console: NONE";
            }
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


    }
}
