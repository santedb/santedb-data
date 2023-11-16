using SanteDB.Core;
using SanteDB.Core.Cdss;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
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
    /// Represents a CDSS library repository
    /// </summary>
    public class AdoCdssLibraryRepository : ICdssLibraryRepository, IMappedQueryProvider<ICdssLibrary>
    {
        // Tracer.
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoJobManager));
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ILocalizationService m_localizationService;
        private readonly IAdhocCacheService m_adhocCache;
        private readonly ModelMapper m_modelMapper;
        private readonly IQueryPersistenceService m_queryPersistence;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoCdssLibraryRepository(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IQueryPersistenceService queryPersistenceService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_localizationService = localizationService;
            this.m_adhocCache = adhocCacheService;
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
        public ICdssLibrary Get(Guid libraryUuid)
        {
            if(libraryUuid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(libraryUuid));
            }

            try
            {
                using(var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open();
                    return this.Get(context, libraryUuid);
                }
            }
            catch(DbException e)
            {
                throw e.TranslateDbException();
            }
            catch(Exception e)
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
            if (this.m_adhocCache?.TryGet<ICdssLibrary>(cacheKey, out library) != true)
            {
                var queryStmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCdssLibrary), typeof(DbCdssLibraryVersion))
                    .InnerJoin<DbCdssLibrary, DbCdssLibraryVersion>(o => o.Key, o => o.Key)
                    .Where<DbCdssLibraryVersion>(o => o.Key == key && o.IsHeadVersion == true);
                var result = context.FirstOrDefault<CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>>(queryStmt.Statement);
                if (result != null)
                {
                    library = this.ToModelInstance(context, result);
                    this.m_adhocCache?.Add(cacheKey, library);
                }
            }
            return library;
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
            if(libraryToInsert == null)
            {
                throw new ArgumentNullException(nameof(libraryToInsert));
            }

            try
            {
                using(var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using(var tx = context.BeginTransaction())
                    {

                        // Is there an existing version?
                        var existingLibrary = context.FirstOrDefault<DbCdssLibrary>(o => o.Key == libraryToInsert.Uuid);
                        if(existingLibrary == null) // Doesn't exist
                        {
                            existingLibrary = context.Insert(new DbCdssLibrary()
                            {
                                Key = libraryToInsert.Uuid,
                                CdssLibraryFormat = libraryToInsert.GetType().AssemblyQualifiedNameWithoutVersion()
                            });
                            libraryToInsert.Uuid = existingLibrary.Key;
                        }
                        else if(Type.GetType(existingLibrary.CdssLibraryFormat) != libraryToInsert.GetType()) // Not allowed to change types
                        {
                            throw new InvalidOperationException(String.Format(ErrorMessages.ARGUMENT_INVALID_TYPE, existingLibrary.CdssLibraryFormat, libraryToInsert.GetType()));
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

                        using(var ms = new MemoryStream())
                        {
                            libraryToInsert.Save(ms);
                            newVersion.Definition = ms.ToArray();
                            
                        }

                        // Is there a current version?
                        var currentVersion = context.Query<DbCdssLibraryVersion>(o => o.Key == existingLibrary.Key).OrderByDescending(o => o.VersionSequenceId).FirstOrDefault();
                        if(currentVersion != null)
                        {
                            if(!SHA1.Create().ComputeHash(newVersion.Definition).SequenceEqual(SHA1.Create().ComputeHash(currentVersion.Definition)))
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
                        this.m_adhocCache?.Add(this.CreateCacheKey(libraryToInsert.Uuid), retVal);
                        return retVal;
                    }
                }
            }
            catch(DbException e)
            {
                throw e.TranslateDbException();
            }
            catch(Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.CDSS_LIBRARY_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<ICdssLibrary, TReturn>> sortExpression)
        {
            return this.m_modelMapper.MapModelExpression<ICdssLibrary, DbCdssLibraryVersion, TReturn>(sortExpression, false);
        }

        /// <inheritdoc/>
        public ICdssLibrary Remove(Guid libraryUuid)
        {
            if(libraryUuid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(libraryUuid));
            }

            try
            {
                using(var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using(var tx = context.BeginTransaction())
                    {

                        var existingRegistration = context.FirstOrDefault<DbCdssLibrary>(o => o.Key == libraryUuid);
                        if(existingRegistration == null)
                        {
                            throw new KeyNotFoundException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, libraryUuid));
                        }

                        // Get the current version
                        var currentVersion = context.FirstOrDefault<DbCdssLibraryVersion>(o => o.IsHeadVersion && o.Key == libraryUuid);
                        if(currentVersion != null)
                        {
                            currentVersion.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                            currentVersion.ObsoletionTime = DateTimeOffset.Now;
                            context.Update(currentVersion);
                        }

                        tx.Commit();
                        this.m_adhocCache?.Remove(this.CreateCacheKey(existingRegistration.Key));

                        return this.ToModelInstance(context, new CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>(existingRegistration, currentVersion));                        
                    }
                }
            }
            catch(DbException e)
            {
                throw e.TranslateDbException();
            }
            catch(Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.CDSS_LIBRARY_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public ICdssLibrary ToModelInstance(DataContext context, object result)
        {
            if (result is CompositeResult<DbCdssLibrary, DbCdssLibraryVersion> cdssResult)
            {
                var retType = Type.GetType(cdssResult.Object1.CdssLibraryFormat);
                if (retType == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.TYPE_NOT_FOUND, cdssResult.Object1.CdssLibraryFormat));
                }

                var retVal = Activator.CreateInstance(retType) as ICdssLibrary;
                using (var ms = new MemoryStream(cdssResult.Object2.Definition))
                {
                    retVal.Load(ms);
                }
                retVal.Uuid = cdssResult.Object1.Key;
                return retVal;
            }
            else
            {
                throw new ArgumentOutOfRangeException(string.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(CompositeResult<DbCdssLibrary, DbCdssLibraryVersion>), result.GetType()));
            }
        }
    }
}
