using SampleConsole.Data;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace SampleConsole
{
    public static class AttributeEx
    {
        public static string GetTableName<T>()
        {
            return ((TableAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(TableAttribute))).Name;
        }

        public static string[] GetColumns<T>(bool includeDatabaseGenerated)
        {
            string[] GetColumnNames((PropertyInfo prop, Attribute column)[] item) => item.Where(x => x.column != null)
                    .Select(x => ((ColumnAttribute)x.column).Name)
                    .ToArray();

            var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
            var tempColumns = props
                .Select(x => (prop: x, column: Attribute.GetCustomAttribute(x, typeof(ColumnAttribute))))
                .ToArray();

            // no ColumnAttribute found
            if (tempColumns.All(x => x.column == null))
                throw new Exception($"None of the columns in {typeof(T)} has {nameof(ColumnAttribute)}.");

            if (includeDatabaseGenerated)
            {
                return GetColumnNames(tempColumns);
            }
            else
            {
                var databaseGenerated = tempColumns
                    .Select(x => (prop: x.prop, column: x.column, attr: Attribute.GetCustomAttribute(x.prop, typeof(DatabaseGeneratedAttribute))))
                    .ToArray();
                // no DatabaseGeneratedAttribute found
                if (databaseGenerated.All(x => x.attr == null))
                {
                    return GetColumnNames(tempColumns);
                }
                else
                {
                    var tempDatabaseColumns = databaseGenerated
                        .Select(x =>
                        {
                            if (x.attr == null)
                                return new { x = x };

                            if (x.attr != null 
                                && x.attr is DatabaseGeneratedAttribute attr
                                && attr.DatabaseGeneratedOption == DatabaseGeneratedOption.None)
                                return new { x = x };
                            
                            return null;
                        })
                        .Where(x => x != null)
                        .Select(x => (prop: x.x.prop, column: x.x.column))
                        .ToArray();
                    return GetColumnNames(tempDatabaseColumns);
                }
            }
        }

        public static RowLevelSecurityAttribute GetRowLevelSecurityAttribute<T>()
        {
            return (RowLevelSecurityAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(RowLevelSecurityAttribute));
        }
    }
}
