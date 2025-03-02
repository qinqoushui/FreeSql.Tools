﻿extern alias mysql69;
using DevComponents.DotNetBar;
using FreeSql.DatabaseModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Windows.Media;

namespace FreeSqlTools
{
    public static class G
    {
        static Dictionary<string, string> keyValues = new Dictionary<string, string>
        {
            {"Oracle","user id={0}; password={1};data source=//{2}:{3}/{4};Pooling=true;Max Pool Size=2" },
            {"MySql","Data Source={2};Port={3};User ID={0};Password={1};Charset=utf8;SslMode=none;Max pool size=2" },
            {"Sqlite","Data Source={2};Pooling=true;Max Pool Size=10" },
            {"PostgreSQL","Host={2};Port={3};Username={0};Password={1};Database={4};Pooling=true;Maximum Pool Size=2" },
            {"SqlServer", "Uid={0};Pwd={1};Initial Catalog={4};Data Source={2}{3};Pooling=true;Max Pool Size=3;Connection Timeout=10;"},
            {"SqlServer1", "Data Source={2}; Integrated Security =True; Initial Catalog={4}; Pooling=true;Max Pool Size=3" }
        };

        static Dictionary<object, Lazy<IFreeSql>> DataBase { get; set; } = new Dictionary<object, Lazy<IFreeSql>>();
        public static string GetConnectionString(FreeSql.DataType dataType, string uid, string pwd,
            string host, string dataName, string port, int valid = 0)
        {
            var connString = keyValues[valid == 0 ? dataType.ToString() : dataType.ToString() + "1"];
            if (host.Length == 1 && host == ".") host = "127.0.0.1";
            connString = string.Format(connString, uid, pwd, host, dataType == FreeSql.DataType.SqlServer ?
                port == "1433" ? ",1433" : $",{port}" : port, dataName);
            return connString;
        }
        public static void AddFreeSql(object key, DataBaseInfo dataBase)
        {
            if (!DataBase.ContainsKey(key))
            {
                var connectionString = dataBase.IsString ? dataBase.ConnectionString
                    : GetConnectionString(dataBase.DataType, dataBase.UserId, dataBase.Pwd, dataBase.Host, dataBase.DbName,
                       dataBase.Port, dataBase.ValidatorType);
                Lazy<IFreeSql> fsql = new Lazy<IFreeSql>(() =>
                {
                    var _fsql = new FreeSql.FreeSqlBuilder()
                          .UseConnectionString(dataBase.DataType, connectionString)
                          .UseLazyLoading(true) //开启延时加载功能
                                                //.UseAutoSyncStructure(true) //自动同步实体结构到数据库              
                          .UseMonitorCommand(
                              cmd => Trace.WriteLine(cmd.CommandText), //监听SQL命令对象，在执行前
                              (cmd, traceLog) => Console.WriteLine(traceLog))
                          .UseLazyLoading(true)
                          .Build();
                    _fsql.Aop.CurdAfter += (s, e) =>
                    {
                        if (e.ElapsedMilliseconds > 200)
                        {
                            //记录日志
                            //发送短信给负责人
                        }
                    };
                    return _fsql;
                });
                DataBase.Add(key, fsql);
            }
        }
        public static void AddFreeSql(object key, string connectionString, FreeSql.DataType dataType)
        {
            if (!DataBase.ContainsKey(key))
            {
                Lazy<IFreeSql> fsql = new Lazy<IFreeSql>(() =>
                {
                    return new FreeSql.FreeSqlBuilder()
                          .UseConnectionString(dataType, connectionString)
                          .UseLazyLoading(true) //开启延时加载功能
                                                //.UseAutoSyncStructure(true) //自动同步实体结构到数据库              
                          .UseMonitorCommand(
                              cmd => Trace.WriteLine(cmd.CommandText), //监听SQL命令对象，在执行前
                              (cmd, traceLog) => Console.WriteLine(traceLog))
                          .UseLazyLoading(true)
                          .Build();
                });
                DataBase.Add(key, fsql);
            }
        }

        public static Lazy<IFreeSql> GetNewFreeSql(DataBaseInfo dataBase)
        {
            var connectionString = dataBase.IsString ? dataBase.ConnectionString
                    : GetConnectionString(dataBase.DataType, dataBase.UserId, dataBase.Pwd, dataBase.Host, dataBase.DbName,
                       dataBase.Port, dataBase.ValidatorType);
            return new Lazy<IFreeSql>(() =>
             {
                 return new FreeSql.FreeSqlBuilder()
                       .UseConnectionString(dataBase.DataType, connectionString)
                       .UseLazyLoading(true) //开启延时加载功能
                                             //.UseAutoSyncStructure(true) //自动同步实体结构到数据库              
                       .UseMonitorCommand(
                           cmd => Trace.WriteLine(cmd.CommandText), //监听SQL命令对象，在执行前
                           (cmd, traceLog) => Console.WriteLine(traceLog))
                       .UseLazyLoading(true)
                       .Build();
             });
            throw new AccessViolationException("FreeSql 连接为空");
        }
        public static IFreeSql GetFreeSql(object key)
        {
            if (DataBase.TryGetValue(key, out Lazy<IFreeSql> fsql))
            {
                return fsql.Value;
            }
            throw new AccessViolationException("FreeSql 连接为空");
        }

        public static List<string> GetDatabases(object key, string connectionString)
        {
            List<string> list = new List<string>();
            try
            {
                if (DataBase.TryGetValue(key, out Lazy<IFreeSql> fsql))
                {
                    list = fsql.Value.DbFirst.GetDatabases();
                }
                return list;
            }
            catch (Exception e)
            {
                TaskDialog.Show("错误提示", eTaskDialogIcon.BlueStop, "数据库连接失败", $" 原因：{e.Message}\n 连接串：{connectionString}\n", eTaskDialogButton.Ok);
                return list;
            }
        }

        public static List<DbTableInfo> GetTablesByDatabase(object key, string dataName)
        {
            List<DbTableInfo> list = new List<DbTableInfo>();
            try
            {
                if (DataBase.TryGetValue(key, out Lazy<IFreeSql> fsql))
                {
                    var ls2 = fsql.Value.DbFirst.GetTablesByDatabase(dataName);
                    if (fsql.Value.Ado.DataType == FreeSql.DataType.MySql)
                        ls2.ForEach(r => r.Schema = dataName);
                    list = ls2.ToList();
                }
                return list;
            }
            catch (Exception e)
            {
                TaskDialog.Show("错误提示", eTaskDialogIcon.Stop, "数据库连接失败", $" 原因：{e.Message}\n 数据库名：{dataName}\n", eTaskDialogButton.Ok);
                return list;
            }
        }

        public static string GetColumnDataTypeDefaultValue(this DbColumnInfo col)
        {
            var s = col.CsType.IsValueType ? Activator.CreateInstance(col.CsType) : null;
            //if (s == null)
            //    return "null";
            //else
            switch (col.CsType.Name)
            {
                case "String":
                    return "''";
                case "Boolean":
                    return "0";
                default:
                    if (s == null)
                        return col.CsType.FullName;
                    else
                        return s.ToString();
                case "DateTime":
                    return $"'{((DateTime)s).ToString("yyyy-MM-dd HH:mm:ss")}'";
            }

        }

        public static string Serialize(this object obj, bool none = false)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj, !none ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
        }
        public static T Deserialize<T>(this string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeserializeAnonymousType<T>(this string json, T type)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(json, type);
        }

        public static List<string> Deserialize(this string json, out List<string> names)
        {
            var token = Newtonsoft.Json.JsonConvert.DeserializeObject(json) as JToken;
            var first = token.First;
            var types = new List<string>();
            var y = new List<(string n, string t)>();
            names = new List<string>();
            while (first != null)
            {
                y.Add(((first as JProperty).Name, ((first as JProperty).Value as JValue).Type.ToString()));
                first = first.Next;
            }
            //排序处理
            foreach (var m in y.OrderBy(r => r.t))
            {
                names.Add(m.n);
                types.Add(m.t);
            }
            return types;
        }

        public static string Table2MD(System.Data.DataTable table)
        {
            StringBuilder stringBuilder = new StringBuilder();
            System.Data.DataColumn[] columns = new System.Data.DataColumn[table.Columns.Count];
            table.Columns.CopyTo(columns, 0);
            stringBuilder.Append("|");
            stringBuilder.Append(string.Join("|", columns.ToList().Select(r => r.ColumnName).ToArray()));
            stringBuilder.AppendLine("|");

            stringBuilder.Append("|");
            stringBuilder.Append(string.Join("|", columns.ToList().Select(r => "--").ToArray()));
            stringBuilder.AppendLine("|");

            foreach (System.Data.DataRow row in table.Rows)
            {
                stringBuilder.AppendLine("|" + string.Join("|", row.ItemArray) + "|");
            }
            return stringBuilder.ToString();
        }

        public static string TableStruct2MD(DbTableInfo dbTableInfo)
        {
            string ss = @"
| 序号 | 列名 | 数据类型 | 长度 | 小数位数 | 主键 | 自增 | 允许空 | 默认值 | 列说明 | 
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | ";
            StringBuilder sb = new StringBuilder($"### {dbTableInfo.Name} {dbTableInfo.Comment}");
            sb.AppendLine(ss);
            int startIndex = dbTableInfo.Columns.Min(r => r.Position);
            dbTableInfo.Columns.OrderBy(r=>r.Position).ToList().ForEach(c =>
            {
                sb.AppendLine($"|{c.Position-startIndex+1}|{c.Name}|{c.DbTypeText}|{c.MaxLength}||{(c.IsPrimary?"Y":"")}|{(c.IsIdentity ? "Y" : "")}|{(c.IsNullable ? "Y" : "")}|{c.DefaultValue}|{c.Coment}");
            });

            return sb.ToString();
            
        }
        public static int ExecuteScript(IFreeSql freesql, string SQLString)
        {
            string currentconn = freesql.Ado.ConnectionString;

            SQLString = SQLString.Replace(Environment.NewLine, "");//去掉换行符
            using (var connection = new mysql69.MySql.Data.MySqlClient.MySqlConnection(currentconn))
            {
                var script = new mysql69.MySql.Data.MySqlClient.MySqlScript(connection);
                try
                {
                    connection.Open();
                    script.Query = SQLString;
                    int rows = script.Execute();
                    return rows;
                }
                catch (MySqlException e)
                {
                    connection.Close();
                    return 0;
                }
            }
        }
    }
}
