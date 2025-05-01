/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2024-6-21
 */
using SanteDB;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Synchronization.ADO.Configuration;
using SanteDB.Persistence.Synchronization.ADO.Model;
using SanteDB.Persistence.Synchronization.ADO.Queues;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SanteDB.Persistence.Synchronization.ADO.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class AdoSynchronizationManager : IServiceImplementation, ISynchronizationLogService, ISynchronizationQueueManager, IProvideBackupAssets, IDisposable
    {
        private readonly Tracer _Tracer;
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
        public IEnumerable<ISynchronizationLogEntry> GetAll()
        {
            using (var conn = _Provider.GetReadonlyConnection())
            {
                conn.Open();
                return conn.Query<DbSynchronizationLogEntry>(e => e.QueryId == null).ToArray(); // ToArray() is called because the conn is disposed 
            }
        }

        /// <inheritdoc />
        public ISynchronizationLogQuery GetCurrentQuery(ISynchronizationLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            using (var conn = _Provider.GetReadonlyConnection())
            {
                conn.Open();
                if (entry is DbSynchronizationLogEntry dbs)
                {
                    return conn.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == dbs.Key && o.QueryId != null);
                }
                else
                {
                    return conn.Query<DbSynchronizationLogEntry>(e => e.ResourceType == entry.ResourceType && e.Filter == entry.Filter && e.QueryId != null).FirstOrDefault();
                }
            }
        }

        /// <inheritdoc />
        public void Delete(ISynchronizationLogEntry entry)
        {
            using (var conn = _Provider.GetWriteConnection())
            {
                conn.Open();
                if (entry is DbSynchronizationLogEntry dbsyncentry)
                {
                    conn.Delete(dbsyncentry);
                }
                else
                {

                    conn.DeleteAll<DbSynchronizationLogEntry>(e => e.ResourceType == entry.ResourceType && e.Filter == e.Filter);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType) => _RegisteredQueues.Where(o => o.Type.HasFlag(queueType) || queueType.HasFlag(o.Type));

        /// <inheritdoc/>
        public ISynchronizationQueue Get(string queueName) => _RegisteredQueues.FirstOrDefault(o => o.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc/>
        public ISynchronizationLogEntry Create(Type modelType, string filter = null)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();
                var modelName = modelType.GetSerializationName();
                var existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.ResourceType == modelName && o.Filter == filter);
                if (existing != null)
                {
                    return existing;
                }
                else
                {
                    return context.Insert(new DbSynchronizationLogEntry()
                    {
                        Filter = filter,
                        ResourceType = modelName
                    });
                }
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogEntry Get(Type modelType, string filter = null)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            using (var context = this._Provider.GetReadonlyConnection())
            {
                context.Open();
                var modelName = modelType.GetSerializationName();
                return context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.ResourceType == modelName && o.Filter == filter);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogEntry Save(ISynchronizationLogEntry entry, string eTag, DateTimeOffset? since)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();

                var existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == entry.Key || (o.ResourceType == entry.ResourceType && o.Filter == entry.Filter));
                if (existing == null)
                {
                    throw new KeyNotFoundException(entry.Key.ToString());
                }

                if (!String.IsNullOrEmpty(eTag))
                {
                    existing.LastETag = eTag;
                }
                existing.LastSync = since;
                existing.LastError = null;
                existing.LastErrorSpecified = true;
                return context.Update(existing);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogEntry SaveError(ISynchronizationLogEntry entry, Exception exception)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            else if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();

                var existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == entry.Key || (o.ResourceType == entry.ResourceType && o.Filter == entry.Filter));
                if (existing == null)
                {
                    throw new KeyNotFoundException(entry.Key.ToString());
                }
                existing.LastError = exception.ToHumanReadableString();
                return context.Update(existing);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogQuery StartQuery(ISynchronizationLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();

                var existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == entry.Key || (o.ResourceType == entry.ResourceType && o.Filter == entry.Filter));
                if (existing == null)
                {
                    throw new KeyNotFoundException(entry.Key.ToString());
                }
                else if (existing.QueryId.HasValue)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(StartQuery)));
                }

                existing.QueryId = Guid.NewGuid();
                existing.QueryOffset = 0;
                existing.QueryStartTime = DateTimeOffset.Now;
                existing.QueryIdSpecified = existing.QueryOffsetSpecified = existing.QueryStartTimeSpecified = true;
                return context.Update(existing);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogQuery SaveQuery(ISynchronizationLogQuery query, int offset)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            else if (query.QueryId == Guid.Empty)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(SaveQuery)));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();
                DbSynchronizationLogEntry existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == query.Key && o.QueryId != null || o.QueryId == query.QueryId);
                if (existing == null)
                {
                    throw new KeyNotFoundException(query.Key.ToString());
                }
                existing.QueryOffset = offset;
                existing.QueryOffsetSpecified = true;
                return context.Update(existing);
            }
        }

        /// <inheritdoc/>
        public void CompleteQuery(ISynchronizationLogQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            else if (query.QueryId == Guid.Empty)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(CompleteQuery)));
            }

            using (var context = this._Provider.GetWriteConnection())
            {
                context.Open();
                DbSynchronizationLogEntry existing = context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == query.Key || o.QueryId == query.QueryId);
                if (existing == null)
                {
                    throw new KeyNotFoundException(query.Key.ToString());
                }
                existing.QueryStartTime = null;
                existing.QueryOffset = null;
                existing.QueryId = null;
                existing.QueryIdSpecified = existing.QueryOffsetSpecified = existing.QueryStartTimeSpecified = true;
                context.Update(existing);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationLogQuery FindQueryData(ISynchronizationLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            using (var context = this._Provider.GetReadonlyConnection())
            {
                context.Open();
                return context.FirstOrDefault<DbSynchronizationLogEntry>(o => o.Key == entry.Key && o.QueryId != null);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IBackupAsset> GetBackupAssets()
        {
            var retVal = this._Configuration.Provider.CreateBackupAsset(Constants.SYNC_DATABASE_ASSET_ID);
            if (retVal != null)
            {
                yield return retVal;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this._Configuration.Provider is IDisposable dispose)
            {
                dispose.Dispose();
            }
        }

    }
}
