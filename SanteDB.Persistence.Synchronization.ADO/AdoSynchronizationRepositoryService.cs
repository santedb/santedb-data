using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Synchronization.ADO.Configuration;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Persistence.Synchronization.ADO
{
    /// <summary>
    /// 
    /// </summary>
    public class AdoSynchronizationRepositoryService : IServiceImplementation, ISynchronizationLogService
    {
        private Tracer _Tracer;

        public string ServiceName => "ADO.NET Synchronization Repository";

        readonly AdoSynchronizationConfigurationSection _Configuration;
        readonly IDataCachingService _DataCachingService;
        readonly IAdhocCacheService _AdhocCache;
        readonly IDbProvider _Provider;

        //private ModelMapper _Mapper;
        //private QueryBuilder _QueryBuilder;

        /// <summary>
        /// Creates a new instance for the repository from Dependency Injection.
        /// </summary>
        public AdoSynchronizationRepositoryService(IDbProvider provider, IQueryPersistenceService queryPersistence, IConfigurationManager configurationManager, IDataCachingService dataCachingService, IAdhocCacheService adhocCache)
        {
            _Tracer = new Tracer(nameof(AdoSynchronizationRepositoryService));
            _Configuration = configurationManager.GetSection<AdoSynchronizationConfigurationSection>();
            _DataCachingService = dataCachingService;
            _AdhocCache = adhocCache;

            try
            {
                _Provider = _Configuration.Provider;
                _Provider.UpgradeSchema("SanteDB.Persistence.Synchronization.ADO");
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
                var result = connection.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == null).FirstOrDefault();

                return result?.LastETag;
            }
        }

        /// <inheritdoc />
        public void Save(Type modelType, string filter, string eTag, DateTime since)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var conn = _Provider.GetWriteConnection())
            {
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
                return conn.Query<Model.DbSynchronizationLogEntry>(e => e.QueryId == null);
            }
        }

        /// <inheritdoc />
        public void SaveQuery(Type modelType, string filter, Guid queryId, int offset)
        {
            var modeltypename = GetModelTypeName(modelType);

            ValidateQueryId(queryId);

            using (var conn = _Provider.GetWriteConnection())
            {
                var record = conn.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == queryId).FirstOrDefault();

                if (null == record)
                {
                    record = new Model.DbSynchronizationLogEntry()
                    {
                        Filter = filter,
                        QueryId = queryId,
                        ResourceType = modeltypename,
                        QueryOffset = offset
                    };
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
        {
            var modeltypename = GetModelTypeName(modelType);

            ValidateQueryId(queryId);

            using (var conn = _Provider.GetWriteConnection())
            {
                conn.DeleteAll<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == queryId);
            }
        }

        /// <inheritdoc />
        public ISynchronizationLogQuery FindQueryData(Type modelType, string filter)
        {
            var modeltypename = GetModelTypeName(modelType);

            using (var conn = _Provider.GetReadonlyConnection())
            {
                return conn.Query<Model.DbSynchronizationLogEntry>(e => e.ResourceType == modeltypename && e.Filter == filter && e.QueryId == null).FirstOrDefault();
            }
        }

        /// <inheritdoc />
        public void Delete(ISynchronizationLogEntry itm)
        {
            using (var conn = _Provider.GetWriteConnection())
            {
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
    }
}
