using SanteDB.Core.Jobs;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Jobs
{
    /// <summary>
    /// Represetns an ADO job state
    /// </summary>
    internal class AdoJobState : IJobState
    {

        /// <summary>
        /// Creates a new job state
        /// </summary>
        public AdoJobState(DbJobState jobState, XmlJobState cacheInfo, IJob targetJob)
        {
            this.LastStartTime = jobState.LastStart?.DateTime;
            this.LastStopTime = jobState.LastStop?.DateTime;
            this.CurrentState = cacheInfo?.CurrentState ?? jobState.LastState;
            this.StatusText = cacheInfo?.StatusText;
            this.Progress = cacheInfo?.Progress ?? 0.0f;
            this.Job = targetJob;
        }

        /// <inheritdoc/>
        public IJob Job { get; }

        /// <inheritdoc/>
        public string StatusText { get; }

        /// <inheritdoc/>
        public float Progress { get; }

        /// <inheritdoc/>
        public JobStateType CurrentState { get; }

        /// <inheritdoc/>
        public DateTime? LastStartTime { get; }

        /// <inheritdoc/>
        public DateTime? LastStopTime { get; }
    }
}
