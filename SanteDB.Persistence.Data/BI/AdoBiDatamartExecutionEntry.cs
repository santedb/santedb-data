using SanteDB.BI.Datamart.DataFlow;
using SanteDB.Core.Model.Interfaces;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Data.BI
{
    /// <summary>
    /// Represetns a BI datamart log entry stored in the database
    /// </summary>
    internal class AdoBiDatamartExecutionEntry : IDataFlowExecutionEntry
    {

        private readonly IDbProvider m_dbProvider;

        /// <summary>
        /// Create a new datamart log entry
        /// </summary>
        public AdoBiDatamartExecutionEntry(DbDatamartExecutionEntry executionEntry, IDbProvider dbProvider)
        {
            this.Key = executionEntry.Key;
            this.CreatedByKey = executionEntry.CreatedByKey;
            this.Finished = executionEntry.EndTime;
            this.Started  = this.ModifiedOn = executionEntry.StartTime;
            this.Purpose = executionEntry.Purpose;
            this.Outcome = executionEntry.Outcome;
            this.m_dbProvider = dbProvider;
        }

        /// <inheritdoc/>
        public Guid? Key { get; }

        /// <inheritdoc/>
        public DataFlowExecutionPurposeType Purpose { get; }

        /// <inheritdoc/>
        public DataFlowExecutionOutcomeType Outcome { get; }

        /// <inheritdoc/>
        public DateTimeOffset Started { get; }

        /// <inheritdoc/>
        public DateTimeOffset? Finished { get; }

        /// <inheritdoc/>
        public Guid? CreatedByKey { get; }

        /// <inheritdoc/>
        public string Tag => $"Execution/{this.Key}";

        /// <inheritdoc/>
        public DateTimeOffset ModifiedOn { get; }

        /// <summary>
        /// Log entries
        /// </summary>
        public IEnumerable<IDataFlowLogEntry> LogEntries
        {
            get
            {
                using(var context = this.m_dbProvider.GetReadonlyConnection())
                {
                    context.Open();
                    return context.Query<DbDatamartLogEntry>(o => o.ExecutionContextId == this.Key).ToList().Select(o => new AdoDatamartLogEntry(o));
                }
            }
        }

        /// <inheritdoc/>
        Guid? IIdentifiedResource.Key
        {
            get => this.Key;
            set => throw new NotSupportedException();
        }

    }
}