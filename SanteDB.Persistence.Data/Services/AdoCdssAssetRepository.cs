using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Core;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Cdss;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Cdss;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents an asset repository which uses the primary database to store assets
    /// </summary>
    public class AdoCdssAssetRepository : ICdssLibraryRepository, IMappedQueryProvider<ICdssAsset>
    {
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IQueryPersistenceService m_queryPersistence;
        private readonly ILocalizationService m_localizationService;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ModelMapper m_modelMapper;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoCdssAssetRepository));
        // Loaded protocols (saves re-constructing and loading them)
        private readonly ConcurrentDictionary<Guid, ICdssAsset> m_protocolCache = new ConcurrentDictionary<Guid, ICdssAsset>();

        /// <summary>
        /// Create a new protocol asset repository
        /// </summary>
        public AdoCdssAssetRepository(IConfigurationManager configurationManager, IQueryPersistenceService queryPersistenceService, ILocalizationService localizationService, IPolicyEnforcementService pepService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_queryPersistence = queryPersistenceService;
            this.m_localizationService = localizationService;
            this.m_pepService = pepService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Clinical Protocol Manager";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistence;

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<ICdssAsset, bool>> query)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<ICdssAsset> Find(Expression<Func<ICdssAsset, bool>> filter)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ICdssAsset Get(Guid protocolAssetId)
        {
            this.m_pepService.Demand(PermissionPolicyIdentifiers.ReadMetadata);
            using(var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                   context.Open();
                    return this.Get(context, protocolAssetId);
                }
                catch (DbException e) 
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
                }
            }
        }

        /// <inheritdoc/>
        public ICdssAsset Get(DataContext context, Guid key)
        {
            var sql = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCdssAssetVersion), typeof(DbCdssAsset))
                       .InnerJoin<DbCdssAssetVersion, DbCdssAsset>(o => o.Key, o => o.Key)
                       .Where<DbCdssAssetVersion>(o => o.Key == key && o.IsHeadVersion);
            var current = context.Query<CompositeResult<DbCdssAsset, DbCdssAssetVersion>>(sql.Statement).FirstOrDefault();
            if (current == null)
            {
                return null;
            }
            else
            {
                return this.ToModelInstance(context, current);
            }
        }

        /// <inheritdoc/>
        public ICdssAsset GetByOid(string protocolAssetOid) => this.Find(o => o.Oid == protocolAssetOid).SingleOrDefault();

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbCdssAssetVersion));
            var headVersion = tableMap.GetColumn(nameof(DbCdssAssetVersion.IsHeadVersion));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{headVersion.Name} = {this.m_configuration.Provider.StatementFactory.CreateSqlKeyword(SqlKeyword.True)}");

        }

        /// <inheritdoc/>
        public ICdssAsset InsertOrUpdate(ICdssAsset protocolAsset)
        {
            if (protocolAsset == null)
            {
                throw new ArgumentNullException(nameof(protocolAsset));
            }
            else if (!(protocolAsset is ICdssProtocol) && !(protocolAsset is ICdssLibrary))
            {
                throw new ArgumentOutOfRangeException(nameof(protocolAsset), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(ICdssProtocol), protocolAsset.GetType()));
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {

                        context.EstablishProvenance(AuthenticationContext.Current.Principal);

                        byte[] protocolData = null;
                        using (var ms = new MemoryStream())
                        {
                            protocolAsset.Save(ms);
                            protocolData = ms.ToArray();
                        }

                        // If this was returned from an ADO asset we want to wrap in a proper classing
                        var handlerClassName = protocolAsset is IProtocolAssetWrapper ap ? ap.Wrapped.GetType().AssemblyQualifiedNameWithoutVersion() : protocolAsset.GetType().AssemblyQualifiedNameWithoutVersion();

                        // First we want to fetch the current version
                        var current = context.Query<DbCdssAssetVersion>(o => o.IsHeadVersion && o.Key == protocolAsset.Uuid).SingleOrDefault();
                        if (current != null &&
                            SHA1.Create().ComputeHash(current.Definition).SequenceEqual(SHA1.Create().ComputeHash(protocolData))) // No changes to the definition
                        {
                            return protocolAsset;
                        }
                        else if (current != null)
                        {
                            var newVersion = new DbCdssAssetVersion();
                            newVersion.CopyObjectData(current);

                            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterClinicalProtocolConfigurationDefinition);
                            current.ObsoletionTime = newVersion.CreationTime = DateTimeOffset.Now;
                            current.ObsoletedByKey = newVersion.CreatedByKey = context.ContextId;
                            current.ObsoletedByKey = null;
                            current.ObsoletionTime = null;
                            current.ObsoletedByKeySpecified = current.ObsoletionTimeSpecified = true;
                            current.IsHeadVersion = false;
                            newVersion.IsHeadVersion = true;
                            newVersion.Definition = protocolData;
                            newVersion.HandlerClass = handlerClassName;
                            newVersion.ReplacesVersionKey = current.VersionKey;
                            context.Update(current);
                            context.Insert(newVersion);
                        }
                        else
                        {
                            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateClinicalProtocolConfigurationDefinition);

                            var rootData = context.Insert(new DbCdssAsset()
                            {
                                Classification = protocolAsset.Classification,
                                Key = protocolAsset.Uuid
                            });

                            // Now we want to construct the version
                            current = context.Insert(new DbCdssAssetVersion()
                            {
                                CreatedByKey = context.ContextId,
                                CreationTime = DateTimeOffset.Now,
                                IsHeadVersion = true,
                                Definition = protocolData,
                                Description = protocolAsset.Documentation,
                                HandlerClass = handlerClassName,
                                Key = rootData.Key,
                                Name = protocolAsset.Name,
                                Id = protocolAsset.Id,
                                Oid = protocolAsset.Oid
                            });
                        }

                        // Clear out all groups and re-assign
                        context.DeleteAll<DbAssetGroupAssociation>(o => o.SourceKey == current.Key);
                        foreach (var grp in protocolAsset.Groups)
                        {
                            var dbGroup = context.FirstOrDefault<DbCdssGroup>(o => (o.Oid == grp.Oid || grp.Name.ToLowerInvariant() == o.Name.ToLowerInvariant() || o.Key == grp.Uuid) && o.ObsoletionTime == null);
                            if (dbGroup == null)
                            {
                                dbGroup = context.Insert(new DbCdssGroup()
                                {
                                    CreatedByKey = context.ContextId,
                                    CreationTime = DateTimeOffset.Now,
                                    Name = grp.Name,
                                    Oid = grp.Oid
                                });
                            }
                            else if (!dbGroup.Name.Equals(grp.Name, StringComparison.InvariantCultureIgnoreCase) ||
                                !dbGroup.Oid.Equals(grp.Oid))
                            {
                                dbGroup.Oid = grp.Oid;
                                dbGroup.Name = grp.Name;
                                dbGroup.UpdatedByKey = context.ContextId;
                                dbGroup.UpdatedTime = DateTimeOffset.Now;
                                context.Update(dbGroup);
                            }

                            context.Insert(new DbAssetGroupAssociation()
                            {
                                GroupKey = dbGroup.Key,
                                SourceKey = current.Key,
                                Key = Guid.NewGuid()
                            });
                        }

                        tx.Commit();

                        this.m_protocolCache.TryRemove(current.Key, out _);
                        ICdssAsset retVal = null;

                        switch (protocolAsset)
                        {
                            case ICdssLibrary lib:
                                retVal = new AdoCdssLibraryAsset(current, lib);
                                break;
                            case ICdssProtocol proto:
                                retVal = new AdoCdssProtocolAsset(current, proto);
                                break;
                            default:
                                throw new InvalidOperationException();
                        }

                        this.m_protocolCache.TryAdd(current.Key, retVal);
                        return retVal;
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
                }
            }
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<ICdssAsset, TReturn>> sortExpression)
        {
            return this.m_modelMapper.MapModelExpression<ICdssAsset, DbCdssAssetVersion, TReturn>(sortExpression, false);
        }

        /// <inheritdoc/>
        public ICdssAsset Remove(Guid protocolAssetId)
        {
            this.m_pepService.Demand(PermissionPolicyIdentifiers.DeleteClinicalProtocolConfigurationDefinition);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {

                        var current = context.Query<DbCdssAssetVersion>(o => o.IsHeadVersion && o.Key == protocolAssetId).SingleOrDefault();
                        if (current == null)
                        {
                            throw new KeyNotFoundException(protocolAssetId.ToString());
                        }
                        current.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        current.ObsoletionTime = DateTimeOffset.Now;
                        current = context.Update(current);
                        tx.Commit();

                        if (!this.m_protocolCache.TryGetValue(protocolAssetId, out var retVal))
                        {
                            switch (context.Query<DbCdssAsset>(o => o.Key == protocolAssetId).Select(o => o.Classification).Single())
                            {
                                case CdssAssetClassification.DecisionSupportLibrary:
                                    retVal = new AdoCdssLibraryAsset(current);
                                    break;
                                case CdssAssetClassification.DecisionSupportProtocol:
                                    retVal = new AdoCdssProtocolAsset(current);
                                    break;
                            }
                        }
                        this.m_protocolCache.TryRemove(protocolAssetId, out _);
                        return retVal;
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
                }
            }
        }

        /// <inheritdoc/>
        public ICdssAsset ToModelInstance(DataContext context, object result)
        {
            switch (result)
            {
                case CompositeResult<DbCdssAsset, DbCdssAssetVersion> cr:
                    return this.ToModelInstance(context, cr);
                case CompositeResult cr2:
                    return this.ToModelInstance(context, new CompositeResult<DbCdssAsset, DbCdssAssetVersion>(cr2.Values.OfType<DbCdssAsset>().First(), cr2.Values.OfType<DbCdssAssetVersion>().First()));
                case DbCdssAssetVersion pv:
                    var rootObj = context.FirstOrDefault<DbCdssAsset>(o => o.Key == pv.Key);
                    return this.ToModelInstance(context, new CompositeResult<DbCdssAsset, DbCdssAssetVersion>(rootObj, pv));
                case DbCdssAsset av:
                    var cVersion = context.FirstOrDefault<DbCdssAssetVersion>(o => o.IsHeadVersion && o.Key == av.Key);
                    return this.ToModelInstance(context, new CompositeResult<DbCdssAsset, DbCdssAssetVersion>(av, cVersion));
                default:
                    throw new InvalidOperationException(SanteDB.Core.i18n.ErrorMessages.MAP_INCOMPATIBLE_TYPE);
            }
        }

        /// <summary>
        /// Convert to appropriate ado wrapper
        /// </summary>
        private ICdssAsset ToModelInstance(DataContext context, CompositeResult<DbCdssAsset, DbCdssAssetVersion> result)
        {
            var groupSql = new SqlStatementBuilder(this.m_configuration.Provider.StatementFactory)
                .SelectFrom(typeof(DbAssetGroupAssociation), typeof(DbCdssGroup))
                .InnerJoin<DbAssetGroupAssociation, DbCdssGroup>(o => o.GroupKey, o => o.Key)
                .Where<DbAssetGroupAssociation>(o => o.SourceKey == result.Object1.Key);

            // Get the asset groups
            if (!this.m_protocolCache.TryGetValue(result.Object1.Key, out var retVal) || retVal.Version != result.Object2.VersionSequenceId.ToString())
            {
                switch (result.Object1.Classification)
                {
                    case CdssAssetClassification.DecisionSupportLibrary:
                        {
                            retVal = new AdoCdssLibraryAsset(result.Object2, context.Query<DbCdssGroup>(groupSql.Statement));
                            this.m_protocolCache.TryRemove(result.Object1.Key, out _);
                            this.m_protocolCache.TryAdd(result.Object1.Key, retVal);
                            return retVal;
                        }
                    case CdssAssetClassification.DecisionSupportProtocol:
                        {
                            retVal = new AdoCdssProtocolAsset(result.Object2, context.Query<DbCdssGroup>(groupSql.Statement));
                            this.m_protocolCache.TryRemove(result.Object1.Key, out _);
                            this.m_protocolCache.TryAdd(result.Object1.Key, retVal);
                            return retVal;
                        }
                }
            }
            return retVal;
        }
    }
}
