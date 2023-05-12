using SanteDB.BI.Datamart.DataFlow;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Diagnostics.Tracing;

namespace SanteDB.Persistence.Data.BI
{
    /// <summary>
    /// Represents a <see cref="IDataFlowLogEntry"/> which is generated from ADO data
    /// </summary>
    internal class AdoDatamartLogEntry : IDataFlowLogEntry
    {

        /// <summary>
        /// Create new log entry
        /// </summary>
        public AdoDatamartLogEntry(DbDatamartLogEntry dbEntry)
        {
            this.Priority = dbEntry.Priority;
            this.Text = dbEntry.Text;
            this.Key = dbEntry.Key;
            this.Timestamp = dbEntry.Timestamp;
        }

        /// <inheritdoc/>
        public EventLevel Priority { get; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc/>
        public string Text { get; }

        /// <inheritdoc/>
        public Guid? Key { get; }

        /// <inheritdoc/>
        public string Tag => $"LogEntry/{this.Key}";

        /// <inheritdoc/>
        public DateTimeOffset ModifiedOn => this.Timestamp;

        /// <inheritdoc/>
        Guid? IIdentifiedResource.Key
        {
            get => this.Key;
            set => throw new NotSupportedException();
        }
    }
}