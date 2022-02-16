using DevComponents.AdvTree;
using DevComponents.DotNetBar;
using FreeSql.DatabaseModel;
using FreeSqlTools.Common;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace FreeSqlTools
{
    public partial class FrmBatch : DevComponents.DotNetBar.Office2007Form
    {
        Node _node;
        FrmLoading frmLoading = null;
        public FrmBatch(Node node)
        {
            this.EnableGlass = false;
            InitializeComponent();
            listBoxAdv2.SelectionMode = eSelectionMode.MultiExtended;
            listBoxAdv1.SelectionMode = eSelectionMode.MultiExtended;


            _node = node;
            Load += FrmBatch_Load;
            this.KeyUp += FrmBatch_KeyUp;
        }

        private void FrmBatch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.O:
                        try
                        {
                            loadExportHistories();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void FrmBatch_Load(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                frmLoading = new FrmLoading();
                frmLoading.ShowDialog();
            });
            labelX3.Text = _node.Parent.Text;
            labelX4.Text = _node.Text;
            LoadTableList();
            loadTemplates();
            Properties.Settings.Default.Reload();
            this.Invoke((Action)delegate { frmLoading.Close(); });

        }

        List<FileInfo> lst = new List<FileInfo>();
        List<DbTableInfo> dbTableInfos = new List<DbTableInfo>();
        void loadTemplates()
        {
            string path = Path.Combine(Environment.CurrentDirectory, "Templates");
            string[] dir = Directory.GetDirectories(path);
            DirectoryInfo fdir = new DirectoryInfo(path);
            FileInfo[] file = fdir.GetFiles("*.tpl");
            if (file.Length != 0 || dir.Length != 0)
            {
                foreach (FileInfo f in file)
                {
                    lst.Add(f);
                    listBoxAdv3.Items.Add(f.Name);
                }
            }
        }
        void LoadTableList()
        {
            dbTableInfos = G.GetTablesByDatabase(_node.Parent.DataKey, _node.Text);
            listBoxAdv1.DataSource = dbTableInfos.Select(a => a.Name).ToArray();
        }



        private void command_all_Executed(object sender, EventArgs e)
        {
            listBoxAdv2.Items.Clear();
            foreach (var m in (string[])listBoxAdv1.DataSource)
                listBoxAdv2.Items.Add(m);
        }

        private void command_unall_Executed(object sender, EventArgs e)
        {
            listBoxAdv2.Items.Clear();
        }

        private void command_select_Executed(object sender, EventArgs e)
        {
            var items = listBoxAdv1.SelectedItems.Select(r=>r.Text).ToList();
            if (items != null)
            {
                items.ToList().ForEach(item =>
                {
                    if (!listBoxAdv2.Items.Cast<string>().Any(a => a == item.ToString()))
                    {
                        listBoxAdv2.Items.Add(item);
                    }
                });
            }
        }

        private void command_unselect_Executed(object sender, EventArgs e)
        {
            var items = listBoxAdv2.SelectedItems.Select(r => r.Text).ToList();
            if (items != null)
            {
                items.ToList().ForEach(item => listBoxAdv2.Items.Remove(item));
            }
        }

        private async void command_export_Executed(object sender, EventArgs e)
        {

            Properties.Settings.Default.Save();
            if (listBoxAdv2.Items.Count == 0)
            {
                MessageBoxEx.Show("请选择表");
                return;
            }
            if (string.IsNullOrEmpty(textBoxX1.Text))
            {
                MessageBoxEx.Show("命名空间不能为空");
                return;
            }
            if (string.IsNullOrEmpty(textBoxX4.Text))
            {
                MessageBoxEx.Show("请选择导出路径");
                return;
            }
            if (listBoxAdv3.CheckedItems.Count == 0)
            {
                MessageBoxEx.Show("请选择生成模板");
                return;
            }
            var templates = listBoxAdv3.CheckedItems.Cast<ListBoxItem>().Select(a => a.Text).ToArray();
            var taskBuild = new TaskBuild()
            {
                Fsql = G.GetFreeSql(_node.DataKey),
                DbName = _node.Text,
                FileName = textBoxX3.Text,
                GeneratePath = textBoxX4.Text,
                NamespaceName = textBoxX1.Text,
                RemoveStr = textBoxX2.Text,
                OptionsEntity01 = checkBoxX1.Checked,
                OptionsEntity02 = checkBoxX2.Checked,
                OptionsEntity03 = checkBoxX3.Checked,
                OptionsEntity04 = checkBoxX4.Checked,
                Templates = templates
            };
            var tables = listBoxAdv2.Items.Cast<string>().ToArray();
            var tableInfos = dbTableInfos.Where(a => tables.Contains(a.Name)).ToList();
            taskBuild.Tables = tableInfos.Select(r => r.Name).ToArray();
            saveExportHistories(taskBuild);
            FrmLoading frmLoading = null;
            ThreadPool.QueueUserWorkItem(new WaitCallback(a =>
            {
                this.Invoke((Action)delegate ()
                {
                    frmLoading = new FrmLoading("正在生成中，请稍后.....");
                    frmLoading.ShowDialog();
                });
            }));
            await new CodeGenerate().Setup(taskBuild, tableInfos);
            this.Invoke((Action)delegate () { frmLoading?.Close(); });

        }
        private void command_openFileDialog_Executed(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxX4.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        #region 导出历史
        void loadExportHistories()
        {
            using OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "导出历史|*.his";
            openFileDialog.FilterIndex = 0;
            openFileDialog.InitialDirectory = Application.StartupPath;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string s = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
                var b = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskBuild>(s);
                _node.Text = b.DbName;
                textBoxX3.Text = b.FileName;
                textBoxX4.Text = b.GeneratePath;
                textBoxX1.Text = b.NamespaceName;
                textBoxX2.Text = b.RemoveStr;
                checkBoxX1.Checked = b.OptionsEntity01;
                checkBoxX2.Checked = b.OptionsEntity02;
                checkBoxX3.Checked = b.OptionsEntity03;
                checkBoxX4.Checked = b.OptionsEntity04;
                for (int i = 0; i < listBoxAdv3.Items.Count; i++)
                {
                    listBoxAdv3.SetItemCheckState(i, b.Templates.Any(r => r == listBoxAdv3.Items[i].ToString()) ? CheckState.Checked : CheckState.Unchecked);

                }
                listBoxAdv2.BeginUpdate();
                listBoxAdv2.Items.Clear();
                if (b.Tables != null)
                    b.Tables.ToList().ForEach(r => listBoxAdv2.Items.Add(r));
                listBoxAdv2.EndUpdate();
            }
        }

        void saveExportHistories(TaskBuild data)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings() { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
            string fileName = Path.Combine(Application.StartupPath, "ExportHistories", data.DbName, data.Templates.First() + ".his");
            if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            File.WriteAllText(fileName, json, Encoding.UTF8);
        }

        #endregion
    }
}
