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
 * User: fyfej
 * Date: 2023-6-21
 */
using SanteDB.BI.Datamart;
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
    /// A <see cref="IDatamart"/> which is derived from ADO.NET storage tables
    /// </summary>
    internal class AdoBiDatamart : IDatamart
    {
        private readonly IDbProvider m_provider;

        public AdoBiDatamart(DbDatamartRegistration datamart,
            IDbProvider dbProvider)
        {
            this.Key = datamart.Key;
            this.CreatedByKey = datamart.CreatedByKey;
            this.CreationTime = datamart.CreationTime;
            this.Description = datamart.Description;
            this.Id = datamart.Id;
            this.Name = datamart.Name;
            this.ObsoletedByKey = datamart.ObsoletedByKey;
            this.ObsoletionTime = datamart.ObsoletionTime;
            this.UpdatedTime = datamart.UpdatedTime;
            this.UpdatedByKey = datamart.UpdatedByKey;
            this.Version = datamart.Version;
            this.DefinitionHash = datamart.DefinitionHash;
            this.m_provider = dbProvider;
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string Version { get; }

        /// <inheritdoc/>
        public IEnumerable<IDataFlowExecutionEntry> FlowExecutions
        {
            get
            {
                using (var context = this.m_provider.GetReadonlyConnection())
                {
                    context.Open();
                    return context.Query<DbDatamartExecutionEntry>(o => o.DatamartKey == this.Key).OrderByDescending(o => o.StartTime).ToList().Select(o => new AdoBiDatamartExecutionEntry(o, this.m_provider));
                }
            }
        }

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
        public byte[] DefinitionHash { get; }

        /// <inheritdoc/>
        public DateTimeOffset? UpdatedTime { get; }

        /// <inheritdoc/>
        public Guid? Key { get; set; }

        /// <inheritdoc/>
        public string Tag => $"DATAMART/{this.Key}";

        /// <inheritdoc/>
        public DateTimeOffset ModifiedOn => this.CreationTime;

        /// <inheritdoc/>
        public void AddAnnotation<T>(T annotation)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IAnnotatedResource CopyAnnotations(IAnnotatedResource other)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAnnotations<T>()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void RemoveAnnotation(object annotation)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void RemoveAnnotations<T>()
        {
            throw new NotSupportedException();
        }
    }
}
