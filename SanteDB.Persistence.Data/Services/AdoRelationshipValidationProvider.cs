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
 * Date: 2023-3-10
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
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
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services
{

    /// <summary>
    /// ADO Relationship validation rule
    /// </summary>
    public class AdoRelationshipValidationRule : IRelationshipValidationRule
    {

        /// <summary>
        /// Create a new relationship validation rule based on a database relationship rule
        /// </summary>
        internal AdoRelationshipValidationRule(DbRelationshipValidationRule rule)
        {
            this.Key = rule.Key;
            this.SourceClassKey = rule.SourceClassKey;
            this.TargetClassKey = rule.TargetClassKey;
            this.RelationshipTypeKey = rule.RelationshipTypeKey;
            this.Description = rule.Description;
        }

        /// <summary>
        /// Create a new validation rule 
        /// </summary>
        internal AdoRelationshipValidationRule(Guid? sourceClassKey, Guid? targetClassKey, Guid relationshipType, String description)
        {
            this.SourceClassKey = sourceClassKey;
            this.TargetClassKey = targetClassKey;
            this.RelationshipTypeKey = relationshipType;
            this.Description = description;
        }

        /// <summary>
        /// The identified key of the relationship.
        /// </summary>
        public Guid? Key { get; }

        /// <summary>
        /// Gets the source classification key
        /// </summary>
        public Guid? SourceClassKey { get; }

        /// <summary>
        /// Gets the target classification key
        /// </summary>
        public Guid? TargetClassKey { get; }

        /// <summary>
        /// Gets the type of relationship
        /// </summary>
        public Guid RelationshipTypeKey { get; }

        /// <summary>
        /// Gets the description of the relationship
        /// </summary>
        public string Description { get; }

    }

    /// <summary>
    /// ADO.NET Based Relationship Provider
    /// </summary>
    /// <remarks>This class allows for the management of validation rules between entities, 
    /// acts, or entities and acts</remarks>
    public class AdoRelationshipValidationProvider : IRelationshipValidationProvider, IMappedQueryProvider<IRelationshipValidationRule>
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoRelationshipValidationProvider));

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IQueryPersistenceService m_queryPersistence;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoRelationshipValidationProvider(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, IQueryPersistenceService queryPersistenceService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_queryPersistence = queryPersistenceService;
        }

        private RelationshipTargetType GetRelationshipTargetType<TRelationship>() where TRelationship : ITargetedAssociation
            => typeof(TRelationship) == typeof(EntityRelationship) ? RelationshipTargetType.EntityRelationship :
                            typeof(TRelationship) == typeof(ActRelationship) ? RelationshipTargetType.ActRelationship : RelationshipTargetType.ActParticipation;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Relationship Validation Service";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistence;

        /// <inheritdoc/>
        public IRelationshipValidationRule AddValidRelationship<TRelationship>(Guid? sourceClassKey, Guid? targetClassKey, Guid relationshipTypeKey, string description)
            where TRelationship : ITargetedAssociation
        {

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterSystemConfiguration);
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {

                try
                {

                    context.Open();
                    var dbInstance = new DbRelationshipValidationRule()
                    {
                        Description = description,
                        RelationshipTypeKey = relationshipTypeKey,
                        SourceClassKey = sourceClassKey,
                        TargetClassKey = targetClassKey,
                        RelationshipClassType = GetRelationshipTargetType<TRelationship>()
                    };

                    return new AdoRelationshipValidationRule(context.Insert(dbInstance));
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
        public IRelationshipValidationRule GetRuleByKey(Guid key) 
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
                    return new AdoRelationshipValidationRule(rule);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IRelationshipValidationRule> GetValidRelationships<TRelationship>()
            where TRelationship : ITargetedAssociation
        {
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                var tclass = GetRelationshipTargetType<TRelationship>();
                context.Open();

                foreach (var itm in context.Query<DbRelationshipValidationRule>(o => o.RelationshipClassType == tclass))
                {
                    yield return new AdoRelationshipValidationRule(itm);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IRelationshipValidationRule> GetValidRelationships<TRelationship>(Guid sourceClassKey)
            where TRelationship : ITargetedAssociation
        {
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                var tclass = GetRelationshipTargetType<TRelationship>();
                context.Open();

                foreach (var itm in context.Query<DbRelationshipValidationRule>(o => (o.SourceClassKey == sourceClassKey || o.SourceClassKey == null) && o.RelationshipClassType == tclass))
                {
                    yield return new AdoRelationshipValidationRule(itm);
                }
            }
        }

        /// <inheritdoc/>
        public IRelationshipValidationRule RemoveRuleByKey(Guid key)
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
                    var existing = context.FirstOrDefault<DbRelationshipValidationRule>(r => r.Key == key);
                    if(existing == null)
                    {
                        throw new KeyNotFoundException(key.ToString());
                    }
                    context.Delete(existing);
                    return new AdoRelationshipValidationRule(existing);
                }
                catch(DbException dbex)
                {
                    throw dbex.TranslateDbException();
                }
                catch(Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
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

                    var tclass = GetRelationshipTargetType<TRelationship>();
                    context.DeleteAll<DbRelationshipValidationRule>(o => o.SourceClassKey == sourceClassKey && o.TargetClassKey == targetClassKey && o.RelationshipTypeKey == relationshipTypeKey && o.RelationshipClassType == tclass);
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
        public IQueryResultSet<IRelationshipValidationRule> QueryRelationships(Expression<Func<IRelationshipValidationRule, bool>> query)
        {
            if(query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            return new MappedQueryResultSet<IRelationshipValidationRule>(this).Where(query);
        }

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<IRelationshipValidationRule, bool>> query)
        {
            throw new NotImplementedException(); // TODO: Implement this
        }

        /// <inheritdoc/>
        public IRelationshipValidationRule Get(DataContext context, Guid key)
        {
            var existing = context.FirstOrDefault<DbRelationshipValidationRule>(o => o.Key == key);
            if(existing != null)
            {
                return new AdoRelationshipValidationRule(existing);
            }
            else
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public IRelationshipValidationRule ToModelInstance(DataContext context, object result)
        {
            if(result is DbRelationshipValidationRule rule)
            {
                return new AdoRelationshipValidationRule(rule);
            }   
            else
            {
                throw new ArgumentOutOfRangeException(nameof(result), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DbRelationshipValidationRule), result.GetType()));
            }
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<IRelationshipValidationRule, TReturn>> sortExpression)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias) => null;
    }
}
