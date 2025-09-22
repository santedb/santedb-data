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
 * Date: 2023-6-21
 */
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence
{
    /// <summary>
    /// Abstract class for versioned associations
    /// </summary>
    public abstract class VersionedAssociationPersistenceService<TModel, TDbModel>
        : IdentifiedDataPersistenceService<TModel, TDbModel>
        where TModel : IdentifiedData, IVersionedAssociation, new()
        where TDbModel : DbIdentified, IDbVersionedAssociation, new()
    {
        /// <summary>
        /// Creates a DI instance of hte persistence layer
        /// </summary>
        public VersionedAssociationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <summary>
        /// Get the current version sequence for the source key
        /// </summary>
        protected abstract long GetCurrentVersionSequenceForSource(DataContext context, Guid sourceKey);

        /// <inheritdoc/>
        protected override bool ValidateCacheItem(TModel cacheEntry, TDbModel dataModel) => cacheEntry.EffectiveVersionSequenceId >= dataModel.EffectiveVersionSequenceId;

        /// <inheritdoc/>
        protected override TModel BeforePersisting(DataContext context, TModel data)
        {
            if (data.SourceEntityKey.HasValue && (!context.PeekData(DataConstants.NoTouchSourceContextKey, out var noTouch) || !(bool)noTouch)) // Touch the entity version
            {
                data.SourceType.GetRelatedPersistenceService().Touch(context, data.SourceEntityKey.Value);
            }
            if(!data.SourceEntityKey.HasValue && data.Key.HasValue)
            {
                data.SourceEntityKey = context.Query<TDbModel>(o=>o.Key == data.Key).Select(o=>o.SourceKey).First();
            }
            data.EffectiveVersionSequenceId = this.GetCurrentVersionSequenceForSource(context, data.SourceEntityKey.Value);
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override TModel DoDeleteModel(DataContext context, Guid key, DeleteMode deleteMode, bool preserveContained)
        {
            var sourceKey = context.Query<TDbModel>(o => o.Key == key).Select(o => o.SourceKey).FirstOrDefault();
            if(sourceKey != Guid.Empty && (!context.PeekData(DataConstants.NoTouchSourceContextKey, out var noTouch) || !(bool)noTouch))
            {
                new TModel().SourceType.GetRelatedPersistenceService().Touch(context, sourceKey);
            }
            return base.DoDeleteModel(context, key, deleteMode, preserveContained);
        }

        /// <summary>
        /// Obsolete all objects
        /// </summary>
        protected override IEnumerable<Guid> DoDeleteAllInternal(DataContext context, Expression<Func<TModel, bool>> expression, DeleteMode deletionMode)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (expression == null)
            {
                throw new ArgumentException(nameof(expression), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_RANGE));
            }

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif

                if (!expression.ToString().Contains(nameof(IVersionedAssociation.ObsoleteVersionSequenceId)))
                {
                    var obsoletionVersionSequenceClause = Expression.MakeMemberAccess(expression.Parameters[0], typeof(TModel).GetProperty(nameof(IVersionedAssociation.ObsoleteVersionSequenceId)));
                    expression = Expression.Lambda<Func<TModel, bool>>(Expression.And(expression.Body, Expression.MakeBinary(ExpressionType.Equal, obsoletionVersionSequenceClause, Expression.Constant(null))), expression.Parameters);
                }

                // Convert the query to a domain query so that the object persistence layer can turn the
                // structured LINQ query into a SQL statement
                var domainExpression = this.m_modelMapper.MapModelExpression<TModel, TDbModel, bool>(expression, false);
                if (domainExpression == null)
                {
                    this.m_tracer.TraceVerbose("WARNING: Using very slow DeleteAll() method - consider using only primary properties for delete all");
                    var columnKey = TableMapping.Get(typeof(TDbModel)).GetColumn(nameof(DbVersionedData.Key));
                    var keyQuery = context.GetQueryBuilder(this.m_modelMapper).CreateQuery(expression, columnKey).Statement;
                    var keys = context.Query<TDbModel>(keyQuery).Select(o => o.Key);
                    domainExpression = o => keys.Contains(o.Key);
                }

                // Get maximum source key
                var sourceKey = context.Query<TDbModel>(domainExpression).OrderByDescending(o => o.EffectiveVersionSequenceId).Select(o => o.SourceKey).FirstOrDefault();
                if (sourceKey == Guid.Empty) // There is no need to delete related objects
                {
                    yield break;
                }

                var sourceSequence = this.GetCurrentVersionSequenceForSource(context, sourceKey);

                var keyReturn = context.Query<TDbModel>(domainExpression).Select(o => o.Key).ToArray();
                switch (deletionMode)
                {
                    case DeleteMode.LogicalDelete:
                        context.UpdateAll<TDbModel>(domainExpression, o => o.ObsoleteVersionSequenceId == sourceSequence);
                        break;
                    default:
                        context.DeleteAll<TDbModel>(domainExpression);
                        break;
                }

                foreach (var itm in keyReturn)
                {
                    yield return itm;
                }
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Obsolete all {0} took {1}ms", expression, sw.ElapsedMilliseconds);
            }
#endif
        }

        /// <summary>
        /// Perform an obsoletion of the association
        /// </summary>
        protected override TDbModel DoDeleteInternal(DataContext context, Guid key, DeleteMode deletionMode)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (key == default(Guid))
            {
                throw new ArgumentException(this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_RANGE, nameof(key)));
            }

            // Versioning in place? if so obsolete is update
            if (this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.AssociationVersioning))
            {
                var existing = context.FirstOrDefault<TDbModel>(o => o.Key == key);
                // Get the source table
                switch (deletionMode)
                {
                    case DeleteMode.LogicalDelete:
                        existing.ObsoleteVersionSequenceId = this.GetCurrentVersionSequenceForSource(context, existing.SourceKey);
                        return this.DoUpdateInternal(context, existing);
                    default:
                        context.Delete(existing);
                        return existing;
                }
            }
            else
            {
                return base.DoDeleteInternal(context, key, deletionMode);
            }
        }

        /// <summary>
        /// Perform an insertion of the object
        /// </summary>
        protected override TDbModel DoInsertInternal(DataContext context, TDbModel dbModel)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (dbModel == null)
            {
                throw new ArgumentNullException(nameof(dbModel), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            // Effective seq set?
            if (dbModel.EffectiveVersionSequenceId == default(int))
            {
                dbModel.EffectiveVersionSequenceId = this.GetCurrentVersionSequenceForSource(context, dbModel.SourceKey);
            }

            
            return base.DoInsertInternal(context, dbModel);
        }

        /// <summary>
        /// Perform an updation of the object
        /// </summary>
        protected override TDbModel DoUpdateInternal(DataContext context, TDbModel dbModel)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (dbModel == null)
            {
                throw new ArgumentNullException(nameof(dbModel), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            // Effective seq set?
            if (dbModel.EffectiveVersionSequenceId == default(int))
            {
                dbModel.EffectiveVersionSequenceId = this.GetCurrentVersionSequenceForSource(context, dbModel.SourceKey);
            }
            if (dbModel.ObsoleteVersionSequenceId.GetValueOrDefault() == Int32.MaxValue)
            {
                dbModel.ObsoleteVersionSequenceId = this.GetCurrentVersionSequenceForSource(context, dbModel.SourceKey);
            }
            dbModel.ObsoleteVersionSequenceIdSpecified = true;

            return base.DoUpdateInternal(context, dbModel);
        }

        /// <inheritdoc/>
        protected override Expression<Func<TModel, bool>> ApplyDefaultQueryFilters(Expression<Func<TModel, bool>> query)
        {
            // TODO: Write a utility function that looks for this
            if (!query.ContainsPropertyReference(nameof(IVersionedAssociation.ObsoleteVersionSequenceId)))
            {
                var obsoletionVersionSequenceClause = Expression.MakeMemberAccess(query.Parameters[0], typeof(TModel).GetProperty(nameof(IVersionedAssociation.ObsoleteVersionSequenceId)));
                query = Expression.Lambda<Func<TModel, bool>>(Expression.And(query.Body, Expression.MakeBinary(ExpressionType.Equal, obsoletionVersionSequenceClause, Expression.Constant(null))), query.Parameters);
            }
            return base.ApplyDefaultQueryFilters(query);
        }

        /// <summary>
        /// Perform a query (appends the filter for obsolete sequence)
        /// </summary>
        protected override OrmResultSet<TDbModel> DoQueryInternal(DataContext context, Expression<Func<TModel, bool>> query, bool allowCache = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (query == null)
            {
                throw new ArgumentNullException(nameof(query), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            return base.DoQueryInternal(context, query, allowCache);
        }
    }
}