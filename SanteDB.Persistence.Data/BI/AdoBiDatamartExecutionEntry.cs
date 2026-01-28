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
 * Date: 2023-6-21
 */
using SanteDB.BI.Datamart.DataFlow;
using SanteDB.Core.Model.Interfaces;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Model.Sys;
using System;
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
            this.Started = this.ModifiedOn = executionEntry.StartTime;
            this.Purpose = executionEntry.Purpose;
            this.Outcome = executionEntry.Outcome;
            this.DiagnosticSessionKey = executionEntry.DiagnosticStreamKey;
            this.m_dbProvider = dbProvider;
        }

        /// <inheritdoc/>
        public Guid? Key { get; }

        /// <inheritdoc/>
        public DataFlowExecutionPurposeType Purpose { get; }

        /// <inheritdoc/>
        public DataFlowExecutionOutcomeType Outcome { get; }

        /// <inheritdoc/>
        public Guid? DiagnosticSessionKey { get; }

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
                using (var context = this.m_dbProvider.GetReadonlyConnection())
                {
                    context.Open(initializeExtensions: false);
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