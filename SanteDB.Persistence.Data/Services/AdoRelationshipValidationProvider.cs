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
using DocumentFormat.OpenXml.EMMA;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
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
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace SanteDB.Persistence.Data.Services
{

    
    /// <summary>
    /// ADO.NET Based Relationship Provider
    /// </summary>
    /// <remarks>This class allows for the management of validation rules between entities, 
    /// acts, or entities and acts</remarks>
    public class AdoRelationshipValidationProvider : IRelationshipValidationProvider, IMappedQueryProvider<RelationshipValidationRule>, IAdoPersistenceProvider<RelationshipValidationRule>, IAdoTrimProvider
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoRelationshipValidationProvider));

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IQueryPersistenceService m_queryPersistence;
        private readonly ModelMapper m_mapper;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoRelationshipValidationProvider(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, IQueryPersistenceService queryPersistenceService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_queryPersistence = queryPersistenceService;
            this.m_mapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
            this.Provider = this.m_configuration.Provider;
        }

        private RelationshipTargetType GetRelationshipTargetType<TRelationship>() where TRelationship : ITargetedAssociation => this.GetRelationshipTargetType(typeof(TRelationship));
            
        private RelationshipTargetType GetRelationshipTargetType(Type tRelationship)
        {
            if(!Enum.TryParse<RelationshipTargetType>(tRelationship.Name, out var retVal))
            {
                throw new ArgumentOutOfRangeException(nameof(tRelationship));
            }
            return retVal;
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Relationship Validation Service";

        /// <inheritdoc/>
        public IDbProvider Provider { get; set; }

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistence;

        /// <inheritdoc/>
        public RelationshipValidationRule AddValidRelationship<TRelationship>(Guid? sourceClassKey, Guid? targetClassKey, Guid relationshipTypeKey, string description)
            where TRelationship : ITargetedAssociation
        {

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterSystemConfiguration);
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {

                try
                {

                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);

                        var retVal = this.Insert(context, new RelationshipValidationRule()
                        {
                            AppliesTo = typeof(TRelationship),
                            RelationshipTypeKey = relationshipTypeKey,
                            SourceClassKey = sourceClassKey,
                            TargetClassKey = targetClassKey,
                            Description = description
                        });
                        tx.Commit();

                        return retVal;
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException($"Error creating validation rule {sourceClassKey}-[{relationshipTypeKey}]->{targetClassKey}", e);
                }

            }
        }

        /// <inheritdoc />
        public RelationshipValidationRule GetRuleByKey(Guid key)
        {
            if (key == Guid.Empty)
            {
                return null;
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                context.Open();

                var rule = context.Query<DbRelationshipValidationRule>(r => r.Key == key).FirstOrDefault();

                if (null == rule)
                {
                    return null;
                }
                else
                {
                    return this.ToModelInstance(context, rule);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<RelationshipValidationRule> GetValidRelationships<TRelationship>()
            where TRelationship : ITargetedAssociation
        {
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                var tclass = GetRelationshipTargetType<TRelationship>();
                context.Open();

                foreach (var itm in context.Query<DbRelationshipValidationRule>(o => o.RelationshipClassType == tclass && o.ObsoletionTime == null))
                {
                    yield return this.ToModelInstance(context, itm);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<RelationshipValidationRule> GetValidRelationships<TRelationship>(Guid sourceClassKey)
            where TRelationship : ITargetedAssociation
        {
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                var tclass = GetRelationshipTargetType<TRelationship>();
                context.Open();

                foreach (var itm in context.Query<DbRelationshipValidationRule>(o => (o.SourceClassKey == sourceClassKey || o.SourceClassKey == null) && o.RelationshipClassType == tclass && o.ObsoletionTime == null))
                {
                    yield return this.ToModelInstance(context, itm);
                }
            }
        }

        /// <inheritdoc/>
        public RelationshipValidationRule RemoveRuleByKey(Guid key)
        {
            if (key == Guid.Empty)
            {
                return null;
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {
                      
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        var retVal = this.Delete(context, key, DeleteMode.PermanentDelete);
                        tx.Commit();
                        return retVal;
                    }
                }
                catch (DbException dbex)
                {
                    throw dbex.TranslateDbException();
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new DataPersistenceException($"Error removing validation rule with id {key}", ex);
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveValidRelationship<TRelationship>(Guid? sourceClassKey, Guid? targetClassKey, Guid relationshipTypeKey)
            where TRelationship : ITargetedAssociation
        {
            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterSystemConfiguration);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    using(var tx = context.BeginTransaction())
                    {
                        var tclass = GetRelationshipTargetType<TRelationship>();
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        var toDelete = context.FirstOrDefault<DbRelationshipValidationRule>(o => o.SourceClassKey == sourceClassKey && o.TargetClassKey == targetClassKey && o.RelationshipTypeKey == relationshipTypeKey && o.RelationshipClassType == tclass);
                        if(toDelete != null)
                        {
                            this.Delete(context, toDelete.Key, DeleteMode.PermanentDelete);
                        }
                        tx.Commit();
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException($"Error removing validation rule {sourceClassKey}-[{relationshipTypeKey}]->{targetClassKey}", e);
                }
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<RelationshipValidationRule> QueryRelationships(Expression<Func<RelationshipValidationRule, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (!query.ToString().Contains(nameof(BaseEntityData.ObsoletionTime)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(query.Parameters[0], typeof(RelationshipValidationRule).GetProperty(nameof(BaseEntityData.ObsoletionTime))), Expression.Constant(null));
                query = Expression.Lambda<Func<RelationshipValidationRule, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, query.Body), query.Parameters);
            }
            return new MappedQueryResultSet<RelationshipValidationRule>(this).Where(query);
        }

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<RelationshipValidationRule, bool>> query)
        {
            var dbQuery = this.m_mapper.MapModelExpression<RelationshipValidationRule, DbRelationshipValidationRule, bool>(query);
            
            return context.Query<DbRelationshipValidationRule>(dbQuery);
        }

        /// <inheritdoc/>
        public RelationshipValidationRule Get(DataContext context, Guid key)
        {
            var existing = context.FirstOrDefault<DbRelationshipValidationRule>(o => o.Key == key && o.ObsoletionTime == null);
            if (existing != null)
            {
                return this.m_mapper.MapDomainInstance<DbRelationshipValidationRule, RelationshipValidationRule>(existing);
            }
            else
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public RelationshipValidationRule ToModelInstance(DataContext context, object result)
        {
            if (result is DbRelationshipValidationRule rule)
            {
                var retVal = this.m_mapper.MapDomainInstance<DbRelationshipValidationRule, RelationshipValidationRule>(rule);
                retVal.AppliesToXml = rule.RelationshipClassType.ToString();

                switch(DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
                {
                    case LoadMode.FullLoad:
                        retVal.CreatedBy = retVal.CreatedBy.GetRelatedPersistenceService().Get(context, rule.CreatedByKey);
                        retVal.ObsoletedBy = retVal.CreatedBy.GetRelatedPersistenceService().Get(context, rule.ObsoletedByKey.GetValueOrDefault());
                        retVal.SourceClass = retVal.SourceClass.GetRelatedPersistenceService().Get(context, rule.SourceClassKey.GetValueOrDefault());
                        retVal.TargetClass = retVal.SourceClass.GetRelatedPersistenceService().Get(context, rule.TargetClassKey.GetValueOrDefault());
                        retVal.RelationshipType = retVal.SourceClass.GetRelatedPersistenceService().Get(context, rule.RelationshipTypeKey);
                        break;
                }

                return retVal;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(result), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DbRelationshipValidationRule), result.GetType()));
            }
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<RelationshipValidationRule, TReturn>> sortExpression)
        {
            return this.m_mapper.MapModelExpression<RelationshipValidationRule, DbRelationshipValidationRule, TReturn>(sortExpression);
        }

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias) => null;

        /// <inheritdoc/>
        public IQueryResultSet<RelationshipValidationRule> Query(DataContext context, Expression<Func<RelationshipValidationRule, bool>> filter) =>
            new MappedQueryResultSet<RelationshipValidationRule>(this, context).Where(filter);

        /// <inheritdoc/>
        public RelationshipValidationRule Insert(DataContext context, RelationshipValidationRule data)
        {
            // UNIQUE Constraint -> We want to remove any previous reference to the same source/target/relationship
            var dbInstance = new DbRelationshipValidationRule()
            {
                Key = data.Key ?? Guid.NewGuid(),
                Description = data.Description,
                RelationshipTypeKey = data.RelationshipTypeKey,
                SourceClassKey = data.SourceClassKey,
                TargetClassKey = data.TargetClassKey,
                RelationshipClassType = this.GetRelationshipTargetType(data.AppliesTo),
                CreatedByKey = context.ContextId,
                CreationTime = DateTimeOffset.Now
            };

            context.DeleteAll<DbRelationshipValidationRule>(o => o.SourceClassKey == dbInstance.SourceClassKey && o.TargetClassKey == dbInstance.TargetClassKey && o.RelationshipTypeKey == dbInstance.RelationshipTypeKey);
            var retVal = context.Insert(dbInstance);
            return this.ToModelInstance(context, dbInstance);
        }

        /// <inheritdoc/>
        public RelationshipValidationRule Update(DataContext context, RelationshipValidationRule data)
        {
            var existing = context.FirstOrDefault<DbRelationshipValidationRule>(o => o.Key == data.Key);
            if(existing == null)
            {
                throw new KeyNotFoundException(data.Key?.ToString());
            }

            existing.ObsoletionTime = null;
            existing.ObsoletedByKey = null;
            existing.ObsoletionTimeSpecified = existing.ObsoletedByKeySpecified = true;
            existing.CreationTime = DateTimeOffset.Now;
            existing.CreatedByKey = context.ContextId;
            existing.SourceClassKey = data.SourceClassKey;
            existing.TargetClassKey = data.TargetClassKey;
            existing.RelationshipTypeKey = data.RelationshipTypeKey;
            existing.Description = data.Description;
            existing.RelationshipClassType = this.GetRelationshipTargetType(data.AppliesTo);
            context.Update(existing);
            return this.ToModelInstance(context, existing);
        }

        /// <inheritdoc/>
        public RelationshipValidationRule Delete(DataContext context, Guid key, DeleteMode deletionMode)
        {
            var existing = context.FirstOrDefault<DbRelationshipValidationRule>(r => r.Key == key);
            switch (deletionMode)
            {
                case DeleteMode.PermanentDelete:
                    context.Delete(existing);
                    break;
                case DeleteMode.LogicalDelete:
                    existing.ObsoletionTime = DateTimeOffset.Now;
                    existing.ObsoletedByKey = context.ContextId;
                    context.Update(existing);
                    break;
            }
            return this.ToModelInstance(context, existing);


        }

        /// <inheritdoc/>
        public RelationshipValidationRule Touch(DataContext context, Guid id)
        {
            var existing = context.FirstOrDefault<DbRelationshipValidationRule>(o => o.Key == id);
            existing.CreationTime = DateTimeOffset.Now;
            existing.CreatedByKey = context.ContextId;
            context.Update(existing);
            return this.ToModelInstance(context, existing);

        }

        /// <inheritdoc/>
        public IdentifiedData Insert(DataContext context, IdentifiedData data)
        {
            if(data is RelationshipValidationRule rel)
            {
                return this.Insert(context, rel);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(data), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(RelationshipValidationRule), data.GetType()));
            }
        }

        /// <inheritdoc/>
        public IdentifiedData Update(DataContext context, IdentifiedData data)
        {
            if (data is RelationshipValidationRule rel)
            {
                return this.Update(context, rel);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(data), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(RelationshipValidationRule), data.GetType()));
            }
        }

        /// <inheritdoc/>
        IdentifiedData IAdoPersistenceProvider.Delete(DataContext context, Guid key, DeleteMode deletionMode) => this.Delete(context, key, deletionMode);

        /// <inheritdoc/>
        public bool Exists(DataContext context, Guid key) => context.Any<DbRelationshipValidationRule>(o => o.Key == key);

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<Type, Guid>> Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            context.DeleteAll<DbRelationshipValidationRule>(o => o.ObsoletionTime != null && o.ObsoletionTime < deletedCutoff);
            yield break;
        }
    }
}
