// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.SqlServer.Server;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Database.Utility
{
    internal sealed class EventEntryDataReader : IDataReader, IConvertible
    {
        private readonly string instanceName;
        private IEnumerator<EventEntry> enumerator;
        private int recordsAffected;
        private SqlDataRecord currentRecord;

        public EventEntryDataReader(IEnumerable<EventEntry> collection, string instanceName)
        {
            this.enumerator = collection.GetEnumerator();
            this.instanceName = instanceName;
        }

        #region IDataReader

        public int Depth
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsClosed
        {
            get
            {
                return this.enumerator == null;
            }
        }

        public int RecordsAffected
        {
            get
            {
                return this.recordsAffected;
            }
        }

        public int FieldCount
        {
            get
            {
                return EventEntryExtensions.Fields.Length;
            }
        }

        public object this[string name]
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public object this[int i]
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Close()
        {
            this.Dispose();
            this.enumerator = null;
        }

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public bool NextResult()
        {
            return this.Read();
        }

        public bool Read()
        {
            bool result = this.enumerator.MoveNext();
            if (result)
            {
                this.recordsAffected++;
                this.currentRecord = this.enumerator.Current.ToSqlDataRecord(this.instanceName);
            }

            return result;
        }

        public void Dispose()
        {
            this.enumerator.Dispose();
        }

        public bool GetBoolean(int i)
        {
            throw new NotImplementedException();
        }

        public byte GetByte(int i)
        {
            throw new NotImplementedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }

        public string GetDataTypeName(int i)
        {
            throw new NotImplementedException();
        }

        public DateTime GetDateTime(int i)
        {
            throw new NotImplementedException();
        }

        public decimal GetDecimal(int i)
        {
            throw new NotImplementedException();
        }

        public double GetDouble(int i)
        {
            throw new NotImplementedException();
        }

        public Type GetFieldType(int i)
        {
            throw new NotImplementedException();
        }

        public float GetFloat(int i)
        {
            throw new NotImplementedException();
        }

        public Guid GetGuid(int i)
        {
            throw new NotImplementedException();
        }

        public short GetInt16(int i)
        {
            throw new NotImplementedException();
        }

        public int GetInt32(int i)
        {
            throw new NotImplementedException();
        }

        public long GetInt64(int i)
        {
            throw new NotImplementedException();
        }

        public string GetName(int i)
        {
            return EventEntryExtensions.Fields[i];
        }

        public int GetOrdinal(string name)
        {
            return Array.IndexOf<string>(EventEntryExtensions.Fields, name);
        }

        public string GetString(int i)
        {
            throw new NotImplementedException();
        }

        public object GetValue(int i)
        {
            return this.currentRecord.GetValue(i);
        }

        public int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public bool IsDBNull(int i)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IConvertible

        public TypeCode GetTypeCode()
        {
            throw new NotImplementedException();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            var list = new List<SqlDataRecord>();

            while (this.enumerator.MoveNext())
            {
                list.Add(this.enumerator.Current.ToSqlDataRecord(this.instanceName));
            }

            this.enumerator.Reset();

            return list;
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
