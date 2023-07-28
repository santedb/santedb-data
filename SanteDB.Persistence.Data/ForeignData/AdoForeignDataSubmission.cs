/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            IEnumerable<DbForeignDataStageParameter> parameters,
            IDataStreamManager dataStreamManager)
        {
            this.Name = foreignDataStage.Name;
            this.Status = foreignDataStage.Status;
            this.ModifiedOn = foreignDataStage.UpdatedTime ?? foreignDataStage.CreationTime;
            this.Tag = foreignDataStage.Key.ToString();
            this.Key = foreignDataStage.Key;
            this.Issues = issues.Select(o => new DetectedIssue(o.Priority, o.LogicalId, o.Text, o.IssueTypeKey)).ToList();
            this.m_streamManager = dataStreamManager;
            this.m_rejectKey = foreignDataStage.RejectStreamKey;
            this.m_sourceKey = foreignDataStage.SourceStreamKey;
            this.ForeignDataMapKey = foreignDataStage.ForeignDataMapKey;
            this.CreatedByKey = foreignDataStage.CreatedByKey;
            this.CreationTime = foreignDataStage.CreationTime;
            this.ObsoletedByKey = foreignDataStage.ObsoletedByKey;
            this.ObsoletionTime = foreignDataStage.ObsoletionTime;
            this.UpdatedByKey = foreignDataStage.UpdatedByKey;
            this.UpdatedTime = foreignDataStage.UpdatedTime;
            this.Description = foreignDataStage.Description;
            this.ParameterValues = parameters.ToDictionary(o => o.Name, o => o.Value);
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
        public Guid? CreatedByKey { get; }

        /// <inheritdoc/>
        public Guid? ObsoletedByKey { get; }

        /// <inheritdoc/>
        public DateTimeOffset CreationTime { get; }

        /// <inheritdoc/>
        public DateTimeOffset? ObsoletionTime { get; }

        /// <inheritdoc/>
        public Guid? UpdatedByKey { get; }

        /// <inheritdoc/>
        public DateTimeOffset? UpdatedTime { get; }

        /// <inheritdoc/>
        public IDictionary<String, String> ParameterValues { get; }

        /// <inheritdoc/>
        public string Description { get; }

        public void AddAnnotation<T>(T annotation)
        {
            throw new NotImplementedException();
        }

        public IAnnotatedResource CopyAnnotations(IAnnotatedResource other)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetAnnotations<T>()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Stream GetRejectStream() => this.m_rejectKey.HasValue ? this.m_streamManager.Get(this.m_rejectKey.Value) : null;

        /// <inheritdoc/>
        public Stream GetSourceStream() => this.m_streamManager.Get(this.m_sourceKey);

        public void RemoveAnnotation(object annotation)
        {
            throw new NotImplementedException();
        }

        public void RemoveAnnotations<T>()
        {
            throw new NotImplementedException();
        }
    }
}
