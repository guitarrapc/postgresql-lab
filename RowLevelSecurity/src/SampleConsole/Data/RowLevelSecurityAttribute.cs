using System;

namespace SampleConsole.Data
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RowLevelSecurityAttribute : Attribute
    {
        /// <summary>
        /// Table Name to use RLS
        /// </summary>
        public string TableName { get; }
        /// <summary>
        /// Column Name to determine RLS key
        /// </summary>
        public string ColumnName { get; }
        /// <summary>
        /// Force Row Level Security
        /// </summary>
        public bool Force { get; }

        public RowLevelSecurityAttribute(string tableName, string columnName = "tenant_id", bool force = true)
        {
            TableName = tableName;
            ColumnName = columnName;
            Force = force;
        }
    }
}
