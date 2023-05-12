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
                    return context.Query<DbDatamartExecutionEntry>(o => o.DatamartKey == this.Key).OrderByDescending(o => o.StartTime).Take(25).ToList().Select(o => new AdoBiDatamartExecutionEntry(o, this.m_provider));
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
