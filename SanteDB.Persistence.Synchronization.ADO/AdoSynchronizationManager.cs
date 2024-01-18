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
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Synchronization.ADO.Configuration;
using SanteDB.Persistence.Synchronization.ADO.Model;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using SanteDB.Persistence.Data;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using System.Collections.Concurrent;
using SanteDB.Persistence.Data.Services.Persistence;
using SanteDB.OrmLite;
using SanteDB.Core.Security.Audit;
using SharpCompress;
using SanteDB.Core.Model.Map;

namespace SanteDB.Persistence.Synchronization.ADO
{
    /// <summary>
    /// 
    /// </summary>
    public class AdoSynchronizationManager : IServiceImplementation, ISynchronizationLogService, ISynchronizationQueueManager
    {
        private Tracer _Tracer;

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Synchronization Repository";

        private readonly AdoSynchronizationConfigurationSection _Configuration;
        private readonly IDataCachingService _DataCachingService;
        private readonly IAdhocCacheService _AdhocCache;
        private readonly IDbProvider _Provider;
        private readonly IReadOnlyCollection<AdoSynchronizationQueue> _RegisteredQueues;

        //private ModelMapper _Mapper;
        //private QueryBuilder _QueryBuilder;
        /// <summary>
        /// Creates a new instance for the repository from Dependency Injection.
        /// </summary>
        public AdoSynchronizationManager(IQueryPersistenceService queryPersistence, IConfigurationManager configurationManager, IDataCachingService dataCachingService, IAdhocCacheService adhocCache, IDataStreamManager dataStreamManager)
        {
            _Tracer = new Tracer(nameof(AdoSynchronizationManager));
            _Configuration = configurationManager.GetSection<AdoSynchronizationConfigurationSection>();
            _DataCachingService = dataCachingService;
            _AdhocCache = adhocCache;

            try
            {
                _Provider = _Configuration.Provider;
                _Provider.UpgradeSchema("SanteDB.Persistence.Synchronization.ADO");

                using (var mapStream = typeof(AdoSynchronizationQueue).Assembly.GetManifestResourceStream("SanteDB.Persistence.Synchronization.ADO.Map.ModelMap.xml"))
                {
                    var mapper = new ModelMapper(mapStream, "SynchronizationMap", true);
                    // Load all queues 
                    using (var ctx = _Provider.GetReadonlyConnection())
                    {
                        ctx.Open();
                        _RegisteredQueues = new ConcurrentBag<AdoSynchronizationQueue>(ctx.Query<DbSynchronizationQueue>(o => true)
                            .ToArray()
                            .Select(o => new AdoSynchronizationQueue(this, dataStreamManager, _Provider, mapper, o)));
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error upgrading schema for synchronization database: {0}", ex.Message);

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private string GetModelTypeName(Type modelType)
        {
            if (null == modelType)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            return modelType.GetCustomAttribute<XmlTypeAttribute>()?.TypeName ?? modelType?.Name;
        }

        /// <summary>
        /// Checks the query id against known bad query ids. This does not validate a query in the database.
        /// </summary>
        /// <param name="queryId"></param>
        /// <exception cref="ArgumentException"></exception>
        [DebuggerHidden]
        private void ValidateQueryId(Guid queryId)
        {
            if (queryId == Guid.Empty)
            {
                throw new ArgumentException("queryId cannot be an empty guid.", nameof(queryId));
            }
        }

        /// <inheritdoc />
        public DateTime? GetLastTime(Type modelType, string filter = null)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var connection = _Provider.GetReadonlyConnection())
            {
                connection.Open();
                var result = connection.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == null).FirstOrDefault();

                return result?.LastSync?.ToLocalTime();
            }
        }

        /// <inheritdoc />
        public string GetLastEtag(Type modelType, string filter = null)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var connection = _Provider.GetReadonlyConnection())
            {
                connection.Open();
                var result = connection.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == null).FirstOrDefault();

                return result?.LastETag;
            }
        }

        /// <inheritdoc />
        public void Save(Type modelType, string filter, string eTag, DateTime? since)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                var record = conn.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == null).FirstOrDefault();

                if (null == record)
                {
                    record = new Model.DbSynchronizationLogEntry()
                    {
                        Filter = filter,
                        LastETag = eTag,
                        LastSync = since,
                        ResourceType = modeltypename
                    };

                    conn.Insert(record);
                }
                else
                {
                    record.LastSync = since;
                    record.LastError = null;
                    record.LastErrorSpecified = true;
                    if (!string.IsNullOrEmpty(eTag))
                    {
                        record.LastETag = eTag;
                    }

                    conn.Update(record);
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<ISynchronizationLogEntry> GetAll()
        {
            using (var conn = _Provider.GetReadonlyConnection())
            {
                conn.Open();
                return conn.Query<Model.DbSynchronizationLogEntry>(e => e.QueryId == null).ToArray(); // ToArray() is called because the conn is disposed 
            }
        }

        /// <inheritdoc />
        public void SaveQuery(Type modelType, string filter, Guid queryId, int offset)
        {
            var modeltypename = GetModelTypeName(modelType);

            ValidateQueryId(queryId);

            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                var record = conn.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == queryId).FirstOrDefault();

                if (null == record)
                {
                    record = conn.Insert(new Model.DbSynchronizationLogEntry()
                    {
                        Filter = filter,
                        QueryId = queryId,
                        ResourceType = modeltypename,
                        QueryOffset = offset
                    });
                }
                else
                {
                    record.QueryOffset = offset;

                    conn.Update(record);
                }
            }
        }

        /// <inheritdoc />
        public void CompleteQuery(Type modelType, string filter, Guid queryId)
            => CompleteQuery(GetModelTypeName(modelType), filter, queryId);

        /// <inheritdoc />
        public void CompleteQuery(string modelType, string filter, Guid queryId)
        {
            ValidateQueryId(queryId);

            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                var existing = conn.FirstOrDefault<DbSynchronizationLogEntry>(o => o.ResourceType == modelType && o.Filter == filter);
                if (existing != null)
                {
                    existing.QueryId = null;
                    existing.QueryOffset = null;
                    existing.QueryStartTime = null;
                    existing.QueryIdSpecified = existing.QueryOffsetSpecified = existing.QueryStartTimeSpecified = true;
                    conn.Update(existing);
                }
            }
        }

        /// <inheritdoc />
        public ISynchronizationLogQuery FindQueryData(Type modelType, string filter)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var conn = _Provider.GetReadonlyConnection())
            {
                conn.Open();
                return conn.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId != null).FirstOrDefault();
            }
        }

        /// <inheritdoc />
        public void Delete(ISynchronizationLogEntry itm)
        {
            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                if (itm is Model.DbSynchronizationLogEntry dbsyncentry)
                {
                    conn.Delete(dbsyncentry);
                }
                else if (itm is ISynchronizationLogQuery lq)
                {
                    conn.DeleteAll<Model.DbSynchronizationLogEntry>(e => e.ResourceType == itm.ResourceType && e.Filter == e.Filter && e.QueryId == lq.QueryId);
                }
                else
                {

                    conn.DeleteAll<Model.DbSynchronizationLogEntry>(e => e.ResourceType == itm.ResourceType && e.Filter == e.Filter && e.QueryId == null);
                }
            }
        }

        /// <inheritdoc />
        public void SaveError(Type modelType, string filter, Exception exception)
        {
            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                var existing = conn.FirstOrDefault<DbSynchronizationLogEntry>(o => o.ResourceType == modelType.Name && o.Filter == filter);
                if (existing != null)
                {
                    existing.LastError = exception.ToHumanReadableString();
                    conn.Update(existing);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType) => this._RegisteredQueues.Where(o =>o.Type.HasFlag(queueType) || queueType.HasFlag(o.Type));

        /// <inheritdoc/>
        public ISynchronizationQueue Get(string queueName) => this._RegisteredQueues.FirstOrDefault(o => o.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));
         
    }
}
