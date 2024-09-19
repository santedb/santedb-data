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