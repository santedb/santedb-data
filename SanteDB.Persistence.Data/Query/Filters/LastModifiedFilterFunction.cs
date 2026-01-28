/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2024-6-21
 */
using SanteDB.OrmLite;
using SanteDB.OrmLite.Providers;
using SanteDB.OrmLite.Providers.Postgres;
using SanteDB.OrmLite.Providers.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
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

            var tableMapping = TableMapping.Get(tableName.Replace(".",""));
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
                    if(tableMapping == null || tableMapping.Columns.Any(c=>c.Name == "upd_utc"))
                    {
                        currentBuilder.Append($"COALESCE({tableName}upd_utc, {tableName}crt_utc) {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));
                    }
                    else
                    {
                        currentBuilder.Append($"{tableName}crt_utc {op} ?", QueryBuilder.CreateParameterValue(value, typeof(DateTimeOffset)));

                    }
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
