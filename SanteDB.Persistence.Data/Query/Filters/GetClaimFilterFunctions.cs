/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Core.i18n;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Providers;
using SanteDB.OrmLite.Providers.Postgres;
using SanteDB.OrmLite.Providers.Sqlite;
using System;

namespace SanteDB.Persistence.Data.Query.Filters
{
    /// <summary>
    /// Generic class for claim filters
    /// </summary>
    public abstract class GetClaimFilterFunction : IDbFilterFunction
    {
        /// <inheritdoc/>
        public abstract string Provider { get; }

        /// <inheritdoc/>
        public string Name => "getClaim";

        /// <inheritdoc/>
        public SqlStatementBuilder CreateSqlStatement(SqlStatementBuilder currentBuilder, string filterColumn, string[] parms, string operand, Type operandType)
        {
            if (parms.Length != 1)
            {
                throw new ArgumentException(String.Format(ErrorMessages.ARGUMENT_COUNT_MISMATCH, 1, parms.Length));
            }

            String claimTable = String.Empty, joinColumn = String.Empty;
            // Determine join on column name
            switch (filterColumn)
            {
                case "sec_usr_tbl.usr_id":
                case "usr_id":
                    claimTable = "SEC_USR_CLM_TBL";
                    joinColumn = $"{claimTable}.USR_ID";
                    break;
                case "sec_app_tbl.app_id":
                case "app_id":
                    claimTable = "SEC_APP_CLM_TBL";
                    joinColumn = $"{claimTable}.APP_ID";
                    break;
                case "sec_dev_tbl.dev_id":
                case "dev_id":
                    claimTable = "SEC_DEV_CLM_TBL";
                    joinColumn = $"{claimTable}.DEV_ID";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(ErrorMessages.ARGUMENT_OUT_OF_RANGE, filterColumn, "usr_id, app_id, dev_id");
            }

            currentBuilder.Append($"EXISTS (SELECT TRUE FROM {claimTable} WHERE CLM_TYP = ? AND LOWER(CLM_VAL) = LOWER(?) AND {joinColumn} = {filterColumn})", parms[0], operand);
            return currentBuilder;

        }
    }


    /// <summary>
    /// <see cref="GetClaimFilterFunction"/> for postgresql
    /// </summary>
    public class PostgresGetClaimFilterFunction : GetClaimFilterFunction
    {
        /// <inheritdoc/>
        public override string Provider => PostgreSQLProvider.InvariantName;
    }

    /// <summary>
    /// <see cref="GetClaimFilterFunction"/> for sqlite
    /// </summary>
    public class SqliteGetClaimFilterFunction : GetClaimFilterFunction
    {
        /// <inheritdoc/>
        public override string Provider => SqliteProvider.InvariantName;
    }
}
