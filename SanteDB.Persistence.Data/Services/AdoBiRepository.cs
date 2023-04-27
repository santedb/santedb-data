using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2010.Ink;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Sys;
using SharpCompress;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents a repository for BI assets in the database
    /// </summary>
    public class AdoBiRepository : IBiMetadataRepository
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoBiRepository));
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IAppletManagerService m_appletManager;
        private readonly IAppletSolutionManagerService m_appletSolutionManager;
        private readonly IAdhocCacheService m_adhocCacheService;
        private readonly ILocalizationService m_localizationService;
        private readonly ModelMapper m_modelMapper;
        private readonly SHA1 m_sha = SHA1.Create();

        /// <summary>
        /// Query provider
        /// </summary>
        private class MappedQueryProvider<TBisDefinition> : IMappedQueryProvider<TBisDefinition>
            where TBisDefinition : BiDefinition
        {

            private readonly IAdhocCacheService m_adhocCacheService;
            private readonly ModelMapper m_modelMapper;

            public MappedQueryProvider(IDbProvider provider, ModelMapper mapper, IAdhocCacheService cacheService)
            {
                this.Provider = provider;
                this.m_modelMapper = mapper;
                this.m_adhocCacheService = cacheService;
            }

            /// <inheritdoc/>
            public IDbProvider Provider { get; }

            /// <inheritdoc/>
            public IQueryPersistenceService QueryPersistence => throw new NotSupportedException();

            private string GetCacheKey(string id) => $"bi.ado.{typeof(TBisDefinition).Name}.{id}";
            private string GetCacheKey(Guid id) => $"bi.ado.{typeof(TBisDefinition).Name}.{id}";

            /// <inheritdoc/>
            public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<TBisDefinition, bool>> query)
            {

                var domainQuery = this.m_modelMapper.MapModelExpression<TBisDefinition, bool>(query, typeof(DbBiQueryResult), true);
                if (!domainQuery.ToString().Contains(nameof(DbVersionedData.ObsoletionTime)))
                {
                    var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(domainQuery.Parameters[0], typeof(DbBiDefinitionVersion).GetProperty(nameof(DbBiDefinitionVersion.ObsoletionTime))), Expression.Constant(null));
                    domainQuery = Expression.Lambda<Func<DbBiQueryResult, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, domainQuery.Body), domainQuery.Parameters);
                }

                var typeName = typeof(TBisDefinition).GetSerializationName();

                // Statement for query
                var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinitionVersion), typeof(DbBiDefinition))
                    .InnerJoin<DbBiDefinitionVersion, DbBiDefinition>(o => o.Key, o => o.Key)
                    .Where(domainQuery)
                    .And<DbBiDefinitionVersion>(o => o.IsHeadVersion)
                    .And<DbBiDefinition>(o=>o.Type == typeName)
                    .Statement
                    .Prepare();

                return context.Query<DbBiQueryResult>(stmt);
            }

            /// <inheritdoc/>
            public TBisDefinition Get(DataContext context, Guid key)
            {

                TBisDefinition retVal = null;
                if (this.m_adhocCacheService?.TryGet(this.GetCacheKey(key), out retVal) == true)
                {
                    return retVal;
                }

                var bidef = context.FirstOrDefault<DbBiDefinition>(o => o.Key == key);
                if (bidef == null)
                {
                    return default(TBisDefinition);
                }
                else if (this.m_adhocCacheService?.TryGet(this.GetCacheKey(bidef.Id), out retVal) != true)
                {
                    var def = context.FirstOrDefault<DbBiDefinitionVersion>(o => o.Key == key && o.IsHeadVersion);
                    using (var ms = new MemoryStream(def.DefinitionContents))
                    {
                        retVal = (TBisDefinition)BiDefinition.Load(ms);
                        this.m_adhocCacheService?.Add(this.GetCacheKey(bidef.Id), retVal);
                        this.m_adhocCacheService?.Add(this.GetCacheKey(key), retVal);
                    }
                }

                return retVal;
            }

            /// <inheritdoc/>
            public SqlStatement GetCurrentVersionFilter(string tableAlias)
            {
                var tableMap = TableMapping.Get(typeof(TBisDefinition));
                var headColumn = tableMap.GetColumn(nameof(DbBiDefinitionVersion.IsHeadVersion));
                return new SqlStatementBuilder(this.Provider.StatementFactory, $"{tableAlias ?? tableMap.TableName}.{headColumn.Name} = {this.Provider.StatementFactory.CreateSqlKeyword(SqlKeyword.True)}")
                    .Statement;
            }

            /// <inheritdoc/>
            public LambdaExpression MapExpression<TReturn>(Expression<Func<TBisDefinition, TReturn>> sortExpression) => this.m_modelMapper.MapModelExpression<TBisDefinition, TReturn>(sortExpression, typeof(DbBiQueryResult), true);

            /// <inheritdoc/>
            public TBisDefinition ToModelInstance(DataContext context, object result)
            {
                if (result is DbBiQueryResult bir)
                {
                    using (var ms = new MemoryStream(bir.DefinitionContents))
                    {
                        return (TBisDefinition)BiDefinition.Load(ms);
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(result));
                }
            }
        }

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoBiRepository(IConfigurationManager configurationManager,
            IAppletManagerService appletManagerService,
            ILocalizationService localizationService,
            IAdhocCacheService adhocCacheService = null,
            IAppletSolutionManagerService solutionManagerService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_appletManager = appletManagerService;
            this.m_appletSolutionManager = solutionManagerService;
            this.m_adhocCacheService = adhocCacheService;
            this.m_localizationService = localizationService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");

            this.m_appletManager.Changed += AppletManagerContentChanged;

            if (this.m_appletSolutionManager != null)
            {
                this.m_appletSolutionManager.Solutions.ForEach(o => this.ProcessAppletCollection(this.m_appletSolutionManager.GetApplets(o.Meta.Id)));
            }
            else
            {
                this.ProcessAppletCollection(this.m_appletManager.Applets);
            }
        }

        /// <summary>
        /// Process the applet collection and insert them
        /// </summary>
        private void ProcessAppletCollection(ReadonlyAppletCollection appletCollection)
        {
            foreach (var itm in appletCollection.SelectMany(c => c.Assets).Where(o => o.Name.StartsWith("bi/") && o.Name.EndsWith(".xml")))
            {
                try
                {
                    this.m_tracer.TraceInfo("Attempting to install {0}...", itm.Name);
                    var content = appletCollection.RenderAssetContent(itm);
                    using (var ms = new MemoryStream(content))
                    {
                        var ast = BiDefinition.Load(ms);
                        this.Insert(ast);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceWarning("Error installing {0}...", itm.Name);
                }
            }
        }

        /// <summary>
        /// Process the applet because it has changed
        /// </summary>
        private void AppletManagerContentChanged(object sender, EventArgs e)
        {
            this.ProcessAppletCollection(this.m_appletManager.Applets);
        }

        /// <inheritdoc/>
        public bool IsLocal => true;

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET BI Metadata Repository";

        /// <inheritdoc/>
        public TBisDefinition Get<TBisDefinition>(string id) where TBisDefinition : BiDefinition
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            try
            {
                var cacheKey = this.GetCacheKey(typeof(TBisDefinition).Name, id);

                TBisDefinition retVal = null;
                if (this.m_adhocCacheService?.TryGet(cacheKey, out retVal) != true)
                {
                    using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                    {
                        context.Open();

                        var typeName = typeof(TBisDefinition).GetSerializationName();
                        var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinition), typeof(DbBiDefinitionVersion))
                            .InnerJoin<DbBiDefinition, DbBiDefinitionVersion>(o => o.Key, o => o.Key)
                            .Where<DbBiDefinitionVersion>(o => o.IsHeadVersion)
                            .And<DbBiDefinition>(o => o.Id == id && o.Type == typeName)
                            .Statement
                            .Prepare();

                        var itm = context.Query<CompositeResult<DbBiDefinition, DbBiDefinitionVersion>>(stmt).FirstOrDefault();
                        if (itm == null)
                        {
                            return null;
                        }

                        using (var ms = new MemoryStream(itm.Object2.DefinitionContents))
                        {
                            retVal = (TBisDefinition)BiDefinition.Load(ms);
                            this.m_adhocCacheService.Add(cacheKey, retVal);
                        }

                    }
                }

                return retVal;

            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.BI_READ_ERR, new { id = id }), e);
            }
        }

        /// <inheritdoc/>
        public TBisDefinition Insert<TBisDefinition>(TBisDefinition metadata) where TBisDefinition : BiDefinition
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {

                        var typeName = metadata.GetType().GetSerializationName();

                        var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinitionVersion), typeof(DbBiDefinition))
                            .InnerJoin<DbBiDefinitionVersion, DbBiDefinition>(o => o.Key, o => o.Key)
                            .Where<DbBiDefinitionVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                            .And<DbBiDefinition>(o => o.Id == metadata.Id && o.Type == typeName)
                            .Statement
                            .Prepare();

                        var existing = context.FirstOrDefault<DbBiQueryResult>(stmt);
                        if (existing != null)
                        {
                            context.EstablishProvenance(AuthenticationContext.Current.Principal);
                            // Is there a need to update this?
                            var existingHash = this.m_sha.ComputeHash(existing.DefinitionContents);
                            using(var ms = new MemoryStream())
                            {
                                metadata.Uuid = existing.Key;// ensure key agreement
                                metadata.Save(ms);
                                var newHash = this.m_sha.ComputeHash(ms.ToArray());
                                if(!existingHash.SequenceEqual(newHash))
                                {
                                    this.m_tracer.TraceWarning("Object {0} already exists - updating instead", metadata);
                                    metadata = this.DoUpdateModel(context, metadata);
                                }
                                else
                                {
                                    return metadata;
                                }
                            }

                        }
                        else
                        {
                            metadata = this.InsertModel(context, metadata);
                        }

                        tx.Commit();
                        this.m_adhocCacheService?.Remove(this.GetCacheKey(metadata.GetType().Name, metadata.Id));
                        this.m_adhocCacheService?.Add(this.GetCacheKey(metadata.GetType().Name, metadata.Id), metadata);
                        return metadata;
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.BI_STORE_ERR, new { id = metadata.Id }), e);
            }
        }

        
        /// <summary>
        /// Get the cache key
        /// </summary>
        private string GetCacheKey(String tbisDefinition, string id) => $"bi.ado.{tbisDefinition}.{id}";

        /// <summary>
        /// Insert a model of the BI definition
        /// </summary>
        private TBisDefinition InsertModel<TBisDefinition>(DataContext context, TBisDefinition metadata) where TBisDefinition : BiDefinition
        {

            if (metadata.Uuid == Guid.Empty)
            {
                metadata.Uuid = Guid.NewGuid();
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            else if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            // define the metadata root
            var biEntry = context.Insert(new DbBiDefinition()
            {
                Key = metadata.Uuid,
                Id = metadata.Id,
                Type = metadata.GetType().GetSerializationName()
            });

            // define the version
            using (var ms = new MemoryStream())
            {
                metadata.Save(ms);
                var biVersion = context.Insert(new DbBiDefinitionVersion()
                {
                    CreatedByKey = context.ContextId,
                    CreationTime = DateTimeOffset.Now,
                    DefinitionContents = ms.ToArray(),
                    IsHeadVersion = true,
                    Name = metadata.Name ?? $"Unnamed {metadata.GetType()}",
                    Key = biEntry.Key,
                    Status = metadata.Status
                });
            }

            return metadata;
        }

        /// <summary>
        /// Perform an update
        /// </summary>
        private TBisDefinition DoUpdateModel<TBisDefinition>(DataContext context, TBisDefinition metadata) where TBisDefinition : BiDefinition
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            else if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var verRoot = metadata.Uuid == Guid.Empty ? context.Query<DbBiDefinition>(o => o.Id == metadata.Id).Select(o => o.Key).First() :
                metadata.Uuid;

            metadata.Uuid = verRoot;
            // Obsolete the current version
            var cVersion = context.Query<DbBiDefinitionVersion>(o => o.IsHeadVersion && o.Key == verRoot).OrderByDescending(o => o.VersionSequenceId).FirstOrDefault();
            var newVersion = new DbBiDefinitionVersion().CopyObjectData<DbBiDefinitionVersion>(cVersion);

            // Is the current version obsolete - if so un-delete
            newVersion.ObsoletionTime = null;
            newVersion.ObsoletedByKey = null;
            newVersion.ObsoletedByKeySpecified = newVersion.ObsoletionTimeSpecified = true;
            newVersion.VersionKey = Guid.Empty;
            newVersion.VersionSequenceId = null;

            // Obsolete and set new version
            cVersion.ObsoletionTime = newVersion.CreationTime = DateTimeOffset.Now;
            cVersion.ObsoletedByKey = newVersion.CreatedByKey = context.ContextId;
            using (var ms = new MemoryStream())
            {
                metadata.Save(ms);
                newVersion.DefinitionContents = ms.ToArray();
            }
            cVersion.IsHeadVersion = false;
            newVersion.IsHeadVersion = true;
            newVersion.Name = metadata.Name ?? $"Unnamed {metadata.GetType()}";
            newVersion.Status = metadata.Status;

            if (this.m_configuration.VersioningPolicy.HasFlag(AdoVersioningPolicyFlags.VersionNonCdrAssets))
            {
                context.Update(cVersion);
                newVersion.ReplacesVersionKey = cVersion.VersionKey;
            }
            else
            {
                context.DeleteAll<DbBiDefinitionVersion>(o => o.Key == metadata.Uuid);
            }
            newVersion = context.Insert(newVersion);

            if (metadata.MetaData == null)
            {
                metadata.MetaData = new BiMetadata()
                {
                    Version = newVersion.VersionSequenceId.ToString()
                };
            }

            return metadata;
        }

        /// <summary>
        /// Returns true if the object is installed
        /// </summary>
        private bool IsInstalled(Type biType, String publicId)
        {
            using(var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                context.Open();
                var typeName = biType.GetSerializationName();
                var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinitionVersion), typeof(DbBiDefinition))
                    .InnerJoin<DbBiDefinitionVersion, DbBiDefinition>(o => o.Key, o => o.Key)
                    .Where<DbBiDefinitionVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                    .And<DbBiDefinition>(o => o.Id == publicId && o.Type == typeName)
                    .Statement
                    .Prepare();
                return context.Any(stmt);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TBisDefinition> Query<TBisDefinition>(Expression<Func<TBisDefinition, bool>> filter, int offset, int? count) where TBisDefinition : BiDefinition => this.Query(filter).Skip(offset).Take(count ?? 100);

        /// <inheritdoc/>
        public IQueryResultSet<TBisDefinition> Query<TBisDefinition>(Expression<Func<TBisDefinition, bool>> filter) where TBisDefinition : BiDefinition
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            try
            {
                try
                {
                    return new MappedQueryResultSet<TBisDefinition>(new MappedQueryProvider<TBisDefinition>(this.m_configuration.Provider, this.m_modelMapper, this.m_adhocCacheService)).Where(filter);
                }
                catch // Fallback to a fat query
                {
                    using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                    {
                        context.Open();

                        var typeName = typeof(TBisDefinition).GetSerializationName();

                        var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinitionVersion), typeof(DbBiDefinition))
                            .InnerJoin<DbBiDefinitionVersion, DbBiDefinition>(o => o.Key, o => o.Key)
                            .Where<DbBiDefinitionVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                            .And<DbBiDefinition>(o => o.Type == typeName)
                            .Statement
                            .Prepare();

                        // TODO: Make this more efficient - the issue is that some queries 
                        return context.Query<CompositeResult<DbBiDefinitionVersion, DbBiDefinition>>(stmt).ToList()
                            .Select(o =>
                            {
                                var cacheKey = this.GetCacheKey(o.Object2.Type, o.Object2.Id);
                                TBisDefinition retVal = null;
                                if (this.m_adhocCacheService?.TryGet(cacheKey, out retVal) != true)
                                {
                                    using (var ms = new MemoryStream(o.Object1.DefinitionContents))
                                    {
                                        retVal = (TBisDefinition)BiDefinition.Load(ms);
                                        this.m_adhocCacheService?.Add(cacheKey, retVal);
                                    }
                                }
                                return retVal;
                            }).Where(filter.Compile()).AsResultSet();

                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.BI_QUERY_ERR, new { filter = filter }), e);
            }
        }

        /// <inheritdoc/>
        public void Remove<TBisDefinition>(string id) where TBisDefinition : BiDefinition
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    var typeName = typeof(TBisDefinition).GetSerializationName();

                    var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbBiDefinition), typeof(DbBiDefinitionVersion))
                        .InnerJoin<DbBiDefinition, DbBiDefinitionVersion>(o => o.Key, o => o.Key)
                        .Where<DbBiDefinitionVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                        .And<DbBiDefinition>(o => o.Id == id && o.Type == typeName)
                        .Statement
                        .Prepare();

                    var existing = context.Query<CompositeResult<DbBiDefinition, DbBiDefinitionVersion>>(stmt).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(id);
                    }

                    // Create a new version 
                    existing.Object2.ObsoletionTime = DateTimeOffset.Now;
                    existing.Object2.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                    context.Update(existing);
                    this.m_adhocCacheService?.Remove(this.GetCacheKey(existing.Object1.Type, id));
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.BI_STORE_ERR, new { id = id }), e);
            }
        }
    }
}
