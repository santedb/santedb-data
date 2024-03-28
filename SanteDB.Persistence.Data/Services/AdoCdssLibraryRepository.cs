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
 * Date: 2023-11-27
 */
using SanteDB.Core;
using SanteDB.Core.Cdss;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http.Compression;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents a CDSS library repository
    /// </summary>
    public class AdoCdssLibraryRepository : ICdssLibraryRepository, IMappedQueryProvider<ICdssLibrary>, IAdoTrimProvider
    {
        // Tracer.
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoJobManager));
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ILocalizationService m_localizationService;
        private readonly ModelMapper m_modelMapper;
        private readonly IQueryPersistenceService m_queryPersistence;
        private readonly ConcurrentDictionary<String, ICdssLibrary> m_cdssLibraryLoaded = new ConcurrentDictionary<string, ICdssLibrary>();

        /// <summary>
        /// Represents an ADO class library entry
        /// </summary>
        private class AdoCdssLibraryEntry : ICdssLibraryRepositoryMetadata
        {

            /// <summary>
            /// Version data
            /// </summary>
            private readonly DbCdssLibraryVersion m_versionData;

            public AdoCdssLibraryEntry(DbCdssLibrary library, DbCdssLibraryVersion version)
            {
                this.m_versionData = version;
            }

            public long? VersionSequence
            {
                get => this.m_versionData.VersionSequenceId;
                set => throw new NotSupportedException();
            }

            public Guid? VersionKey
            {
                get => this.m_versionData.VersionKey;
                set => throw new NotSupportedException();
            }

            public Guid? PreviousVersionKey
            {
                get => this.m_versionData.ReplacesVersionKey;
                set => throw new NotSupportedException();
            }

            public bool IsHeadVersion
            {
                get => this.m_versionData.IsHeadVersion;
                set => throw new NotSupportedException();
            }

            public Guid? Key
            {
                get => this.m_versionData.Key;
                set => throw new NotSupportedException();
            }

            public string Tag => this.VersionKey.ToString();

            public DateTimeOffset ModifiedOn => this.m_versionData.ObsoletionTime ?? this.m_versionData.CreationTime;

            public Guid? CreatedByKey => this.m_versionData.CreatedByKey;

            public Guid? ObsoletedByKey => this.m_versionData.ObsoletedByKey;

            public DateTimeOffset CreationTime => this.m_versionData.CreationTime;

            public DateTimeOffset? ObsoletionTime => this.m_versionData.ObsoletionTime;
        }

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoCdssLibraryRepository(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, ILocalizationService localizationService, IQueryPersistenceService queryPersistenceService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_localizationService = localizationService;
            this.m_queryPersistence = queryPersistenceService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");

        }


        /// <inheritdoc/>
        public string ServiceName => "ADO.NET CDSS LIBRARY MANAGER";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistence;

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<ICdssLibrary, bool>> query)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (query == null)
            {
                throw new ArgumentNullException(nameof(query), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var expression = this.m_modelMapper.MapModelExpression<ICdssLibrary, DbCdssLibraryVersion, bool>(query);
            var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(expression.Parameters[0], typeof(DbCdssLibraryVersion).GetProperty(nameof(DbCdssLibraryVersion.ObsoletionTime))), Expression.Constant(null));
            expression = Expression.Lambda<Func<DbCdssLibraryVersion, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, expression.Body), expression.Parameters);

            var joinedQuery = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCdssLibraryVersion), typeof(DbCdssLibrary))
                .InnerJoin<DbCdssLibraryVersion, DbCdssLibrary>(o => o.Key, o => o.Key)
                .Where<DbCdssLibraryVersion>(expression);
            return context.Query<CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>>(joinedQuery.Statement);
        }

        /// <inheritdoc/>
        public IQueryResultSet<ICdssLibrary> Find(Expression<Func<ICdssLibrary, bool>> filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }
            return new MappedQueryResultSet<ICdssLibrary>(this).Where(filter);
        }

        /// <inheritdoc/>
        public ICdssLibrary Get(Guid libraryUuid, Guid? versionUuuid)
        {
            if (libraryUuid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(libraryUuid));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open();
                    if (versionUuuid.GetValueOrDefault() != Guid.Empty)
                    {
                        return this.Get(context, libraryUuid, versionUuuid);
                    }
                    else
                    {
                        return this.Get(context, libraryUuid);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.CDSS_LIBRARY_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public ICdssLibrary Get(DataContext context, Guid key)
        {
            // Attempt to load from ad-hoc cache first
            var cacheKey = this.CreateCacheKey(key);
            ICdssLibrary library = null;
            if (this.m_cdssLibraryLoaded.TryGetValue(cacheKey, out library) != true)
            {
                library = this.Get(context, key, null);
                this.m_cdssLibraryLoaded.TryAdd(cacheKey, library);
            }
            return library;
        }

        private ICdssLibrary Get(DataContext context, Guid key, Guid? versionKey)
        {
            var queryStmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCdssLibrary), typeof(DbCdssLibraryVersion))
                   .InnerJoin<DbCdssLibrary, DbCdssLibraryVersion>(o => o.Key, o => o.Key);

            if (versionKey.HasValue)
            {
                queryStmt = queryStmt.Where<DbCdssLibraryVersion>(o => o.Key == key && o.VersionKey == versionKey.Value);
            }
            else
            {
                queryStmt = queryStmt.Where<DbCdssLibraryVersion>(o => o.Key == key && o.IsHeadVersion == true);
            }

            var result = context.FirstOrDefault<CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>>(queryStmt.Statement);
            if (result != null)
            {
                return this.ToModelInstance(context, result);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Create a caching key
        /// </summary>
        private string CreateCacheKey(Guid key) => $"cdss.lib.{key}";

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbCdssLibraryVersion));
            var headColumn = tableMap.GetColumn(nameof(DbCdssLibraryVersion.IsHeadVersion));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{headColumn.Name} = {this.Provider.StatementFactory.CreateSqlKeyword(SqlKeyword.True)}");
        }

        /// <inheritdoc/>
        public ICdssLibrary InsertOrUpdate(ICdssLibrary libraryToInsert)
        {
            if (libraryToInsert == null)
            {
                throw new ArgumentNullException(nameof(libraryToInsert));
            }

            bool isSystemContext = AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal;

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {

                        // Is there an existing version?
                        var storageKey = libraryToInsert?.StorageMetadata?.Key;
                        var existingLibrary = context.FirstOrDefault<DbCdssLibrary>(o => o.Key == libraryToInsert.Uuid || o.Key == storageKey);
                        if (existingLibrary == null) // Doesn't exist
                        {
                            if (!isSystemContext)
                            {
                                this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateClinicalProtocolConfigurationDefinition);
                            }
                            existingLibrary = context.Insert(new DbCdssLibrary()
                            {
                                Key = libraryToInsert.Uuid,
                                CdssLibraryFormat = libraryToInsert.GetType().AssemblyQualifiedNameWithoutVersion(),
                                IsSystem = AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal
                            });
                            libraryToInsert.Uuid = existingLibrary.Key;
                        }
                        else if (Type.GetType(existingLibrary.CdssLibraryFormat) != libraryToInsert.GetType()) // Not allowed to change types
                        {
                            throw new InvalidOperationException(String.Format(ErrorMessages.ARGUMENT_INVALID_TYPE, existingLibrary.CdssLibraryFormat, libraryToInsert.GetType()));
                        }
                        else if (existingLibrary.IsSystem &&
                            !isSystemContext)
                        {
                            this.m_pepService.Demand(PermissionPolicyIdentifiers.UnrestrictedAll);
                        }
                        else if (!isSystemContext)
                        {
                            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterClinicalProtocolConfigurationDefinition);
                        }

                        // If there's not material change then we shouldn't persist the change
                        var newVersion = new DbCdssLibraryVersion()
                        {
                            Key = existingLibrary.Key,
                            VersionKey = Guid.NewGuid(),
                            CreationTime = DateTimeOffset.Now,
                            CreatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal),
                            Documentation = libraryToInsert.Documentation,
                            Id = libraryToInsert.Id,
                            Name = libraryToInsert.Name,
                            Oid = libraryToInsert.Oid,
                            VersionName = libraryToInsert.Version,
                            IsHeadVersion = true
                        };

                        using (var ms = new MemoryStream())
                        {
                            using (var cs = CompressionUtil.GetCompressionScheme(Core.Http.Description.HttpCompressionAlgorithm.Gzip).CreateCompressionStream(ms))
                            {
                                libraryToInsert.Save(cs);
                            }
                            newVersion.Definition = ms.ToArray();

                        }

                        // Is there a current version?
                        var currentVersion = context.Query<DbCdssLibraryVersion>(o => o.Key == existingLibrary.Key).OrderByDescending(o => o.VersionSequenceId).FirstOrDefault();
                        if (currentVersion != null)
                        {
                            if (SHA1.Create().ComputeHash(newVersion.Definition).SequenceEqual(SHA1.Create().ComputeHash(currentVersion.Definition)) && !currentVersion.ObsoletionTime.HasValue)
                            {
                                this.m_tracer.TraceWarning("Not updating CDSS definition since it has not changed.");
                                return this.ToModelInstance(context, new CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>(existingLibrary, currentVersion));
                            }
                            currentVersion.ObsoletionTime = newVersion.CreationTime;
                            currentVersion.ObsoletedByKey = newVersion.CreatedByKey;
                            currentVersion.IsHeadVersion = false;
                            newVersion.ReplacesVersionKey = currentVersion.VersionKey;
                            context.Update(currentVersion);
                        }
                        context.Insert(newVersion);

                        tx.Commit();

                        var retVal = this.ToModelInstance(context, new CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>(existingLibrary, newVersion));
                        _ = this.m_cdssLibraryLoaded.TryRemove(this.CreateCacheKey(libraryToInsert.Uuid), out _);
                        _ = this.m_cdssLibraryLoaded.TryAdd(this.CreateCacheKey(libraryToInsert.Uuid), retVal);
                        return retVal;
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.CDSS_LIBRARY_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<ICdssLibrary, TReturn>> sortExpression)
        {
            var sortQuery = this.m_modelMapper.MapModelExpression<ICdssLibrary, DbCdssLibraryVersion, TReturn>(sortExpression, false);
            if(sortQuery == null)
            {
                var propertySelector = QueryExpressionBuilder.BuildPropertySelector(sortExpression).Replace("storage.", ""); // HACK:
                var parameter = Expression.Parameter(typeof(DbCdssLibraryVersion));
                Expression selector = null;
                switch(propertySelector)
                {
                    case "creationTime":
                    case "modifiedOn":
                    case "updatedTime":
                        return Expression.Lambda<Func<DbCdssLibraryVersion, TReturn>>(Expression.Convert(Expression.MakeMemberAccess(parameter, typeof(DbCdssLibraryVersion).GetProperty(nameof(DbCdssLibraryVersion.CreationTime))), typeof(TReturn)), parameter);
                    default:
                        throw new InvalidOperationException();
                }
            }
            return sortQuery;
        }

        /// <inheritdoc/>
        public ICdssLibrary Remove(Guid libraryUuid)
        {
            if (libraryUuid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(libraryUuid));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.DeleteClinicalProtocolConfigurationDefinition);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {
                        var existingRegistration = context.FirstOrDefault<DbCdssLibrary>(o => o.Key == libraryUuid);
                        if (existingRegistration == null)
                        {
                            throw new KeyNotFoundException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, libraryUuid));
                        }

                        // Get the current version
                        var currentVersion = context.FirstOrDefault<DbCdssLibraryVersion>(o => o.IsHeadVersion && o.Key == libraryUuid);
                        if (currentVersion != null)
                        {
                            currentVersion.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                            currentVersion.ObsoletionTime = DateTimeOffset.Now;
                            context.Update(currentVersion);
                        }

                        tx.Commit();
                        _ = this.m_cdssLibraryLoaded.TryRemove(this.CreateCacheKey(existingRegistration.Key), out _);

                        return this.ToModelInstance(context, new CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>(existingRegistration, currentVersion));
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.CDSS_LIBRARY_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public ICdssLibrary ToModelInstance(DataContext context, object result)
        {
            if (result is CompositeResult<DbCdssLibrary, DbCdssLibraryVersion> cdssResult)
            {
                var libraryType = Type.GetType(cdssResult.Object1.CdssLibraryFormat);
                if (libraryType == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.TYPE_NOT_FOUND, cdssResult.Object1.CdssLibraryFormat));
                }
                var libraryInstance = Activator.CreateInstance(libraryType) as ICdssLibrary;
                libraryInstance.StorageMetadata = new AdoCdssLibraryEntry(cdssResult.Object1, cdssResult.Object2);
                using (var ms = new MemoryStream(cdssResult.Object2.Definition))
                {
                    using (var cs = CompressionUtil.GetCompressionScheme(Core.Http.Description.HttpCompressionAlgorithm.Gzip).CreateDecompressionStream(ms))
                    {
                        libraryInstance.Load(cs);
                    }
                }
                return libraryInstance;
            }
            else if (result == null)
            {
                return null;
            }
            else
            {
                throw new ArgumentOutOfRangeException(string.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>), result.GetType()));
            }
        }


        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {

            // Trim out any deleted versions where the head is deleted beyond the deleted cutoff
            foreach (var itm in context.Query<DbCdssLibraryVersion>(o => o.IsHeadVersion && o.ObsoletionTime != null && o.ObsoletionTime < deletedCutoff).ToArray())
            {
                context.Delete(itm);
                context.DeleteAll<DbCdssLibrary>(o => o.Key == itm.Key);
                auditBuilder.WithAuditableObjects(new AuditableObject()
                {
                    IDTypeCode = AuditableObjectIdType.ReportName,
                    ObjectId = itm.Name,
                    LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                    NameData = $"{nameof(DbCdssLibrary)}/{itm.Key}",
                    Type = AuditableObjectType.SystemObject,
                    Role = AuditableObjectRole.Resource,
                    ObjectData = new List<ObjectDataExtension>()
                    {
                        new ObjectDataExtension("cdss.library.name", itm.Name),
                        new ObjectDataExtension("cdss.library.id", itm.Id),
                        new ObjectDataExtension("cdss.library.oid", itm.Oid)
                    }
                });
            }

            // Trim out old versions of BI definitions & prune any deleted 
            context.DeleteAll<DbCdssLibraryVersion>(o => o.ObsoletionTime != null && o.ObsoletionTime < oldVersionCutoff && !o.IsHeadVersion);
        }

    }
}
