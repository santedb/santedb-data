using SanteDB.OrmLite;
using SanteDB.OrmLite.Providers;
using SanteDB.OrmLite.Providers.Postgres;
using SanteDB.OrmLite.Providers.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Query.Filters
{
    /// <summary>
    /// Database implementation for <see cref="LastModifiedFilterFunction"/>
    /// </summary>
    public abstract class LastModifiedFilterFunction : IDbFilterFunction
    {
        /// <inheritdoc/>
        public abstract string Provider { get; }

        /// <inheritdoc/>
        public string Name => "lastModified";

        /// <inheritdoc/>
        public SqlStatementBuilder CreateSqlStatement(SqlStatementBuilder currentBuilder, string filterColumn, string[] parms, string operand, Type operandType)
        {

            var match = Constants.ExtractFilterOperandRegex.Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op))
            {
                op = "=";
            }

            // Extract the filter columns
            match = Constants.ExtractColumnBindingRegex.Match(filterColumn);
            String tableName = match.Groups[1].Value, columnName = match.Groups[2].Value;

            switch (columnName)
            {
                case "ent_id":
                case "ent_vrsn_id":
                    currentBuilder.Append($"ent_vrsn_tbl.crt_utc {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));
                    break;
                case "act_id":
                case "act_vrsn_id":
                    currentBuilder.Append($"act_vrsn_tbl.crt_utc {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));
                    break;
                case "cd_id":
                case "cd_vrsn_id":
                    currentBuilder.Append($"cd_vrsn_tbl.crt_utc {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));
                    break;
                default:
                    currentBuilder.Append($"COALESCE({tableName}upd_utc, {tableName}crt_utc) {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));
                    break;
            }
            return currentBuilder;   
        }
    }

    /// <summary>
    /// Implementation of <see cref="LastModifiedFilterFunction"/> for psql
    /// </summary>
    public class PostgresLastModifiedFilterFunction : LastModifiedFilterFunction
    {
        /// <inheritdoc/>
        public override string Provider => PostgreSQLProvider.InvariantName;
    }

    /// <summary>
    /// Implementation of <see cref="LastModifiedFilterFunction"/> for sqlite
    /// </summary>
    public class SqliteLastModifiedFilterFunction : LastModifiedFilterFunction
    {
        /// <inheritdoc/>
        public override string Provider => SqliteProvider.InvariantName;
    }
}
