using SanteDB.Core.i18n;
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
            if(parms.Length != 1)
            {
                throw new ArgumentException(String.Format(ErrorMessages.ARGUMENT_COUNT_MISMATCH, 1, parms.Length));
            }

            String claimTable = String.Empty, joinColumn = String.Empty;
            // Determine join on column name
            switch(filterColumn)
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
    public class SqliteGetClaimFilterFunction :GetClaimFilterFunction
    {
        /// <inheritdoc/>
        public override string Provider => SqliteProvider.InvariantName;
    }
}
