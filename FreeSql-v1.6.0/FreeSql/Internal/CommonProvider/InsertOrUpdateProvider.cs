﻿using FreeSql.Extensions.EntityUtil;
using FreeSql.Internal.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FreeSql.Internal.CommonProvider
{

    public abstract partial class InsertOrUpdateProvider<T1> : IInsertOrUpdate<T1> where T1 : class
    {
        protected IFreeSql _orm;
        protected CommonUtils _commonUtils;
        protected CommonExpression _commonExpression;
        protected List<T1> _source = new List<T1>();
        protected Dictionary<string, bool> _auditValueChangedDict = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
        protected TableInfo _table;
        protected Func<string, string> _tableRule;
        protected DbParameter[] _params;
        protected DbTransaction _transaction;
        protected DbConnection _connection;
        public ColumnInfo IdentityColumn { get; }

        public InsertOrUpdateProvider(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression)
        {
            _orm = orm;
            _commonUtils = commonUtils;
            _commonExpression = commonExpression;
            _table = _commonUtils.GetTableByEntity(typeof(T1));
            if (_orm.CodeFirst.IsAutoSyncStructure && typeof(T1) != typeof(object)) _orm.CodeFirst.SyncStructure<T1>();
            IdentityColumn = _table.Primarys.Where(a => a.Attribute.IsIdentity).FirstOrDefault();
        }

        protected void ClearData()
        {
            _source.Clear();
            _auditValueChangedDict.Clear();
        }

        public IInsertOrUpdate<T1> WithTransaction(DbTransaction transaction)
        {
            _transaction = transaction;
            _connection = _transaction?.Connection;
            return this;
        }
        public IInsertOrUpdate<T1> WithConnection(DbConnection connection)
        {
            if (_transaction?.Connection != connection) _transaction = null;
            _connection = connection;
            return this;
        }

        public static void AuditDataValue(object sender, IEnumerable<T1> data, IFreeSql orm, TableInfo table, Dictionary<string, bool> changedDict)
        {
            if (data?.Any() != true) return;
            if (orm.Aop.AuditValueHandler == null) return;
            foreach (var d in data)
            {
                if (d == null) continue;
                foreach (var col in table.Columns.Values)
                {
                    object val = col.GetMapValue(d);
                    var auditArgs = new Aop.AuditValueEventArgs(Aop.AuditValueType.InsertOrUpdate, col, table.Properties[col.CsName], val);
                    orm.Aop.AuditValueHandler(sender, auditArgs);
                    if (auditArgs.IsChanged)
                    {
                        col.SetMapValue(d, val = auditArgs.Value);
                        if (changedDict != null && changedDict.ContainsKey(col.Attribute.Name) == false)
                            changedDict.Add(col.Attribute.Name, true);
                    }
                }
            }
        }
        public static void AuditDataValue(object sender, T1 data, IFreeSql orm, TableInfo table, Dictionary<string, bool> changedDict)
        {
            if (orm.Aop.AuditValueHandler == null) return;
            if (data == null) return;
            if (typeof(T1) == typeof(object) && new[] { table.Type, table.TypeLazy }.Contains(data.GetType()) == false)
                throw new Exception($"操作的数据类型({data.GetType().DisplayCsharp()}) 与 AsType({table.Type.DisplayCsharp()}) 不一致，请检查。");
            foreach (var col in table.Columns.Values)
            {
                object val = col.GetMapValue(data);
                var auditArgs = new Aop.AuditValueEventArgs(Aop.AuditValueType.InsertOrUpdate, col, table.Properties[col.CsName], val);
                orm.Aop.AuditValueHandler(sender, auditArgs);
                if (auditArgs.IsChanged)
                {
                    col.SetMapValue(data, val = auditArgs.Value);
                    if (changedDict != null && changedDict.ContainsKey(col.Attribute.Name) == false)
                        changedDict.Add(col.Attribute.Name, true);
                }
            }
        }

        public IInsertOrUpdate<T1> SetSource(T1 source) => this.SetSource(new[] { source });
        public IInsertOrUpdate<T1> SetSource(IEnumerable<T1> source)
        {
            if (source == null || source.Any() == false) return this;
            AuditDataValue(this, source, _orm, _table, _auditValueChangedDict);
            _source.AddRange(source.Where(a => a != null));
            return this;
        }

        protected string TableRuleInvoke()
        {
            if (_tableRule == null) return _table.DbName;
            var newname = _tableRule(_table.DbName);
            if (newname == _table.DbName) return _table.DbName;
            if (string.IsNullOrEmpty(newname)) return _table.DbName;
            if (_orm.CodeFirst.IsSyncStructureToLower) newname = newname.ToLower();
            if (_orm.CodeFirst.IsSyncStructureToUpper) newname = newname.ToUpper();
            if (_orm.CodeFirst.IsAutoSyncStructure) _orm.CodeFirst.SyncStructure(_table.Type, newname);
            return newname;
        }
        public IInsertOrUpdate<T1> AsTable(Func<string, string> tableRule)
        {
            _tableRule = tableRule;
            return this;
        }
        public IInsertOrUpdate<T1> AsType(Type entityType)
        {
            if (entityType == typeof(object)) throw new Exception("IInsertOrUpdate.AsType 参数不支持指定为 object");
            if (entityType == _table.Type) return this;
            var newtb = _commonUtils.GetTableByEntity(entityType);
            _table = newtb ?? throw new Exception("IInsertOrUpdate.AsType 参数错误，请传入正确的实体类型");
            if (_orm.CodeFirst.IsAutoSyncStructure) _orm.CodeFirst.SyncStructure(entityType);
            return this;
        }

        public void WriteSourceSelectUnionAll(List<T1> source, StringBuilder sb, List<DbParameter> dbParams)
        {
            var didx = 0;
            foreach (var d in source)
            {
                if (didx > 0) sb.Append(" \r\nUNION ALL\r\n ");
                sb.Append("SELECT ");
                var colidx2 = 0;
                foreach (var col in _table.Columns.Values)
                {
                    if (colidx2 > 0) sb.Append(", ");
                    if (string.IsNullOrEmpty(col.DbInsertValue) == false)
                        sb.Append(col.DbInsertValue);
                    else
                    {
                        object val = col.GetMapValue(d);
                        sb.Append(_commonUtils.GetNoneParamaterSqlValue(dbParams, col.Attribute.MapType, val));
                    }
                    if (didx == 0) sb.Append(" as ").Append(col.Attribute.Name);
                    ++colidx2;
                }
                switch (_orm.Ado.DataType)
                {
                    case DataType.OdbcOracle:
                    case DataType.Oracle:
                    case DataType.OdbcDameng:
                    case DataType.Dameng:
                        sb.Append(" FROM dual");
                        break;
                }
                ++didx;
            }
        }

        byte _SplitSourceByIdentityValueIsNullFlag = 0; //防止重复计算 SplitSource
        /// <summary>
        /// 如果实体类有自增属性，分成两个 List，有值的Item1 merge，无值的Item2 insert
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public NaviteTuple<List<T1>, List<T1>> SplitSourceByIdentityValueIsNull(List<T1> source)
        {
            if (_SplitSourceByIdentityValueIsNullFlag == 1) return NaviteTuple.Create(source, new List<T1>());
            if (_SplitSourceByIdentityValueIsNullFlag == 2) return NaviteTuple.Create(new List<T1>(), source);
            if (IdentityColumn == null) return NaviteTuple.Create(source, new List<T1>());
            var ret = NaviteTuple.Create(new List<T1>(), new List<T1>());
            foreach (var item in source)
            {
                if (object.Equals(_orm.GetEntityValueWithPropertyName(_table.Type, item, IdentityColumn.CsName), IdentityColumn.CsType.CreateInstanceGetDefaultValue()))
                    ret.Item2.Add(item); //自增无值的，记录为直接插入
                else
                    ret.Item1.Add(item);
            }
            return ret;
        }

        public abstract string ToSql();
        public int ExecuteAffrows()
        {
            var affrows = 0;
            var ss = SplitSourceByIdentityValueIsNull(_source);
            try
            {
                if (_transaction != null)
                {
                    _source = ss.Item1;
                    _SplitSourceByIdentityValueIsNullFlag = 1;
                    affrows += this.RawExecuteAffrows();
                    _source = ss.Item2;
                    _SplitSourceByIdentityValueIsNullFlag = 2;
                    affrows += this.RawExecuteAffrows();
                }
                else
                {
                    using (var conn = _orm.Ado.MasterPool.Get())
                    {
                        _transaction = conn.Value.BeginTransaction();
                        var transBefore = new Aop.TraceBeforeEventArgs("BeginTransaction", null);
                        _orm.Aop.TraceBeforeHandler?.Invoke(this, transBefore);
                        try
                        {
                            _source = ss.Item1;
                            _SplitSourceByIdentityValueIsNullFlag = 1;
                            affrows += this.RawExecuteAffrows();
                            _source = ss.Item2;
                            _SplitSourceByIdentityValueIsNullFlag = 2;
                            affrows += this.RawExecuteAffrows();
                            _transaction.Commit();
                            _orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, "提交", null));
                        }
                        catch (Exception ex)
                        {
                            _transaction.Rollback();
                            _orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, "回滚", ex));
                            throw ex;
                        }
                        _transaction = null;
                    }
                }
            }
            finally
            {
                _SplitSourceByIdentityValueIsNullFlag = 0;
                ClearData();
            }
            return affrows;
        }
        public int RawExecuteAffrows()
        {
            var sql = this.ToSql();
            if (string.IsNullOrEmpty(sql)) return 0;
            var before = new Aop.CurdBeforeEventArgs(_table.Type, _table, Aop.CurdType.InsertOrUpdate, sql, _params);
            _orm.Aop.CurdBeforeHandler?.Invoke(this, before);
            var affrows = 0;
            Exception exception = null;
            try
            {
                affrows = _orm.Ado.ExecuteNonQuery(_connection, _transaction, CommandType.Text, sql, _params);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw ex;
            }
            finally
            {
                var after = new Aop.CurdAfterEventArgs(before, exception, affrows);
                _orm.Aop.CurdAfterHandler?.Invoke(this, after);
            }
            return affrows;
        }
#if net40
#else
        async public Task<int> RawExecuteAffrowsAsync()
        {
            var sql = this.ToSql();
            if (string.IsNullOrEmpty(sql)) return 0;
            var before = new Aop.CurdBeforeEventArgs(_table.Type, _table, Aop.CurdType.InsertOrUpdate, sql, _params);
            _orm.Aop.CurdBeforeHandler?.Invoke(this, before);
            var affrows = 0;
            Exception exception = null;
            try
            {
                affrows = await _orm.Ado.ExecuteNonQueryAsync(_connection, _transaction, CommandType.Text, sql, _params);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw ex;
            }
            finally
            {
                var after = new Aop.CurdAfterEventArgs(before, exception, affrows);
                _orm.Aop.CurdAfterHandler?.Invoke(this, after);
            }
            return affrows;
        }
        async public Task<int> ExecuteAffrowsAsync()
        {
            var affrows = 0;
            var ss = SplitSourceByIdentityValueIsNull(_source);
            try
            {
                if (_transaction != null)
                {
                    _source = ss.Item1;
                    _SplitSourceByIdentityValueIsNullFlag = 1;
                    affrows += await this.RawExecuteAffrowsAsync();
                    _source = ss.Item2;
                    _SplitSourceByIdentityValueIsNullFlag = 2;
                    affrows += await this.RawExecuteAffrowsAsync();
                }
                else
                {
                    using (var conn = await _orm.Ado.MasterPool.GetAsync())
                    {
                        _transaction = conn.Value.BeginTransaction();
                        var transBefore = new Aop.TraceBeforeEventArgs("BeginTransaction", null);
                        _orm.Aop.TraceBeforeHandler?.Invoke(this, transBefore);
                        try
                        {
                            _source = ss.Item1;
                            _SplitSourceByIdentityValueIsNullFlag = 1;
                            affrows += await this.RawExecuteAffrowsAsync();
                            _source = ss.Item2;
                            _SplitSourceByIdentityValueIsNullFlag = 2;
                            affrows += await this.RawExecuteAffrowsAsync();
                            _transaction.Commit();
                            _orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, "提交", null));
                        }
                        catch (Exception ex)
                        {
                            _transaction.Rollback();
                            _orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, "回滚", ex));
                            throw ex;
                        }
                        _transaction = null;
                    }
                }
            }
            finally
            {
                _SplitSourceByIdentityValueIsNullFlag = 0;
                ClearData();
            }
            return affrows;
        }
#endif
    }
}
