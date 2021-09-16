using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevComponents.DotNetBar;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace FreeSqlTools
{
    public class ExportHelper
    {
        static ExportHelper _ = new ExportHelper();
        public static ExportHelper Instance { get; } = _;

        public void ExportMySql_RowsData_Replace(DbDataReader rdr)
        {


            string insertStatementHeader =  "REPLACE INTO `" ;

            var sb = new StringBuilder();
            while (rdr.Read())
            {
                

                string sqlDataRow = Export_GetValueString(rdr, table);

                if (sb.Length == 0)
                {
                    sb.AppendLine(insertStatementHeader);
                    sb.Append(sqlDataRow);
                }
                else if ((long)sb.Length + (long)sqlDataRow.Length < ExportInfo.MaxSqlLength)
                {
                    sb.AppendLine(",");
                    sb.Append(sqlDataRow);
                }
                else
                {
                    sb.AppendFormat(";");

                    Export_WriteLine(sb.ToString(), tw);
                    tw.Flush();

                    sb = new StringBuilder((int)ExportInfo.MaxSqlLength);
                    sb.AppendLine(insertStatementHeader);
                    sb.Append(sqlDataRow);
                }
            }

            rdr.Close();

            if (sb.Length > 0)
            {
                sb.Append(";");
            }

            Export_WriteLine(sb.ToString(), tw);

            sb = null;
        }

        private string Export_GetValueString(MySqlDataReader rdr, MySqlTable table)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string columnName = rdr.GetName(i);

                if (table.Columns[columnName].IsGeneratedColumn)
                    continue;

                if (sb.Length == 0)
                    sb.AppendFormat("(");
                else
                    sb.AppendFormat(",");
                object ob = null;
                try
                {
                    ob = rdr[i];

                }
                catch (Exception ex)
                {
                    switch (rdr.GetFieldType(i).Name)
                    {
                        case "DateTime":
                            ob = DateTime.MinValue;
                            break;
                        default:
                            throw ex;
                    }
                }
                var col = table.Columns[columnName];
                //sb.Append(QueryExpress.ConvertToSqlFormat(rdr, i, true, true, col));
                sb.Append(QueryExpress.ConvertToSqlFormat(ob, true, true, col, ExportInfo.BlobExportMode));
            }

            sb.AppendFormat(")");
            return sb.ToString();
        }

    }
}
