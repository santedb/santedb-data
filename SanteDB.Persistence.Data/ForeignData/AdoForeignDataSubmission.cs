using SanteDB.Core.BusinessRules;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.Persistence.Data.ForeignData
{
    /// <summary>
    /// ADO foreign data information
    /// </summary>
    internal class AdoForeignDataSubmission : IForeignDataSubmission
    {

        private readonly IDataStreamManager m_streamManager;
        private readonly Guid m_sourceKey;
        private readonly Guid? m_rejectKey;

        /// <summary>
        /// Foreign data information 
        /// </summary>
        public AdoForeignDataSubmission(DbForeignDataStage foreignDataStage, 
            IEnumerable<DbForeignDataIssue> issues, 
            IDataStreamManager dataStreamManager)
        {
            this.Name = foreignDataStage.Name;
            this.Status = foreignDataStage.Status;
            this.ModifiedOn = foreignDataStage.UpdatedTime ?? foreignDataStage.CreationTime;
            this.Tag = foreignDataStage.Key.ToString();
            this.Key = foreignDataStage.Key;
            this.Issues = issues.Select(o => new DetectedIssue(o.Priority, o.LogicalId, o.Text, o.IssueTypeKey));
            this.m_streamManager = dataStreamManager;
            this.m_rejectKey = foreignDataStage.RejectStreamKey;
            this.m_sourceKey = foreignDataStage.SourceStreamKey;
            this.ForeignDataMapKey = foreignDataStage.ForeignDataMapKey;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ForeignDataStatus Status { get; }

        /// <inheritdoc/>
        public IEnumerable<DetectedIssue> Issues { get; }

        /// <inheritdoc/>
        public Guid? Key { get; set; }

        /// <inheritdoc/>
        public string Tag { get; }

        /// <inheritdoc/>
        public DateTimeOffset ModifiedOn { get; }

        /// <inheritdoc/>
        public Guid ForeignDataMapKey { get; }

        /// <inheritdoc/>
        public Stream GetRejectStream() => this.m_rejectKey.HasValue ? this.m_streamManager.Get(this.m_rejectKey.Value) : null;

        /// <inheritdoc/>
        public Stream GetSourceStream() => this.m_streamManager.Get(this.m_sourceKey);
    }
}
