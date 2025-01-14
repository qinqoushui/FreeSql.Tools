﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevComponents.AdvTree;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit;
using System.Diagnostics;
using System.Data.SqlTypes;
using MySql.Data.MySqlClient;

namespace FreeSqlTools.Component
{
    public partial class UCDataGrid : UserControl
    {
        private readonly Node _node;
        private readonly Lazy<IFreeSql> fsql;
        private DataBaseInfo dataBaseInfo;
        TextEditor editor = new TextEditor();
        public UCDataGrid(Node node)
        {
            InitializeComponent();
            var typeConverter = new HighlightingDefinitionTypeConverter();
            //展示行号
            editor.ShowLineNumbers = true;
            //editor.Padding = new System.Windows.Thickness(20);
            //字体
            editor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            editor.FontSize = 22;
            editor.KeyDown += (s, e) =>
              {
                  //if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
                  {
                      switch (e.Key)
                      {
                          case System.Windows.Input.Key.F5:
                              buttonItem1_Click(null, null);
                              break;
                          default:
                              break;
                      }
                  }
              };
            //C#语法高亮          
            var csSyntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom("TSQL");
            editor.SyntaxHighlighting = csSyntaxHighlighter;
            //将editor作为elemetnHost的组件
            elementHost1.Child = editor;
            _node = node;
            dataBaseInfo = (DataBaseInfo)(node.Level == 3 ? node.Parent.Parent.Tag :
                 node.Parent.Tag);
            if (dataBaseInfo.DataType == FreeSql.DataType.SqlServer)
            {
                dataBaseInfo.DbName = node.Parent.Text;
            }

            fsql = G.GetNewFreeSql(dataBaseInfo);
            Load += UCDataGrid_Load;
        }

        private void UCDataGrid_Load(object sender, EventArgs e)
        {
            string sqlString = "";
            switch (dataBaseInfo.DataType)
            {
                case FreeSql.DataType.SqlServer:
                    sqlString = $"use [{_node.Parent.Text}];SELECT top 1000 * FROM {_node.Text}"; break;
                case FreeSql.DataType.Oracle:
                    sqlString = $"use {_node.Parent.Text};SELECT * FROM {_node.Text} WHERE ROWNUM <=1000"; break;
                case FreeSql.DataType.MySql:
                case FreeSql.DataType.PostgreSQL:
                    sqlString = $"use `{_node.Parent.Text}`;SELECT * FROM `{_node.Text}` LIMIT 1000";
                    mySqlBackup = new MySqlBackup(fsql.Value.Ado.ConnectionString);
                    mySqlBackup.ExportInfo.RowsExportMode = RowsDataExportMode.Replace;
                    break;
            }
            editor.Text = sqlString;

            BindDataGridView(sqlString);
        }

        void BindDataGridView(string sqlString)
        {
            this.BeginInvoke(QueryBindDataGridView, sqlString);
        }

        Action<string> QueryBindDataGridView => (sqlString) =>
        {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            dataGridViewX1.DataSource = fsql.Value.Ado.ExecuteDataTable(CommandType.Text, sqlString);
            switch (dataBaseInfo.DataType)
            {
                case FreeSql.DataType.MySql:
                case FreeSql.DataType.PostgreSQL:
                    try
                    {
                        textBox1.Text = mySqlBackup.ExportRowsToString(_node.Parent.Text, _node.Text, sqlString);
                        textBoxMD.Text = mySqlBackup.ExportRowsToMarkDownString(_node.Parent.Text, _node.Text, sqlString);
                        textBoxStruct.Text = G.TableStruct2MD(_node.Tag as FreeSql.DatabaseModel.DbTableInfo);
                    }
                    catch (Exception ex)
                    {
                        //从DG中导出MD数据
                        textBox1.Text = String.Empty;
                        textBoxMD.Text = G.Table2MD(dataGridViewX1.DataSource as DataTable);
                        textBoxStruct.Text = String.Empty;
                       MessageBox.Show(ex.Message);
                    }
                    break;
            }
            stopwatch.Stop();
            labelItem1.Text = $"查询耗时：{stopwatch.ElapsedMilliseconds} 毫秒";
        };
        MySqlBackup mySqlBackup = null;

        private void buttonItem1_Click(object sender, EventArgs e)
        {
            if (!editor.Text.StartsWith("use"))
            {
                editor.Text = $"use {_node.Parent.Text};\r\n" + editor.Text;
            }
            this.BeginInvoke(QueryBindDataGridView, editor.Text);
        }


    }
}
