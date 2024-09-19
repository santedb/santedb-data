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
 */
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Data.Quality.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents an implementation of the <see cref="IDataQualityConfigurationProviderService"/> which stores data quality rules in the database.
    /// </summary>
    /// <remarks>This is useful for multi-application server environments where configurations are to be shared between instances</remarks>
    public class AdoDataQualityConfigurationProvider : IDataQualityConfigurationProviderService, IAdoTrimProvider
    {


        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoDataQualityConfigurationProvider));
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IAdhocCacheService m_adhocCache;

        /// <inheritdoc/>
        public AdoDataQualityConfigurationProvider(IConfigurationManager configurationManager,
            ILocalizationService localizationService,
            IPolicyEnforcementService pepService,
            IAdhocCacheService adhocCacheService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_pepService = pepService;
            this.m_adhocCache = adhocCacheService;

            // Import from configuration
            try
            {
                var dqConfigurationSection = configurationManager.GetSection<DataQualityConfigurationSection>();
                if (dqConfigurationSection?.RuleSets.Any() == true)
                {
                    foreach (var rs in dqConfigurationSection.RuleSets)
                    {
                        if (this.GetRuleSet(rs.Id) == null)
                        {
                            this.SaveRuleSet(rs);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Could not copy configuration based rule-set to ADO - {0}", e.ToHumanReadableString());
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Data Quality Configuration Provider";

        /// <inheritdoc/>
        public DataQualityRulesetConfiguration GetRuleSet(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            DataQualityRulesetConfiguration retVal = null;
            if (this.m_adhocCache?.TryGet(this.CreateCacheKey(id), out retVal) == true)
            {
                return retVal;
            }

            try
            {
                using (var ctx = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    ctx.Open();
                    var ruleSet = ctx.FirstOrDefault<DbDataQualityConfiguration>(o => o.Id == id);
                    if (ruleSet == null)
                    {
                        return null;
                    }

                    retVal = this.ConvertToRuleSet(ctx, ruleSet);
                    this.m_adhocCache?.Add(this.CreateCacheKey(id), retVal);
                    return retVal;
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATAQUALITY_CONFIG_READ_ERROR), e);
            }
        }

        /// <summary>
        /// Create a cache key for the ad-hoc cache
        /// </summary>
        private string CreateCacheKey(String id) => $"dq.cnf.{id}";

        /// <summary>
        /// Convert a <paramref name="ruleSet"/> to a configuration object
        /// </summary>
        /// <param name="ctx">The context on which the ruleset was loaded</param>
        /// <param name="ruleSet">The ruleset to convert</param>
        /// <returns>The converted rule set</returns>
        private DataQualityRulesetConfiguration ConvertToRuleSet(DataContext ctx, DbDataQualityConfiguration ruleSet)
        {
            var retVal = new DataQualityRulesetConfiguration()
            {
                Enabled = ruleSet.Enabled,
                Id = ruleSet.Id,
                Name = ruleSet.Name,
                Key = ruleSet.Key,
                CreatedByKey = ruleSet.CreatedByKey,
                ObsoletedByKey = ruleSet.ObsoletedByKey,
                UpdatedByKey = ruleSet.UpdatedByKey,
                CreationTime = ruleSet.CreationTime,
                ObsoletionTime = ruleSet.ObsoletionTime,
                UpdatedTime = ruleSet.UpdatedTime
            };
            retVal.Resources = ctx.Query<DbDataQualityResourceConfiguration>(o => o.DataQualityConfigurationKey == ruleSet.Key).ToArray().Select(o => this.ConvertToResourceConfiguration(ctx, o)).ToList();
            return retVal;
        }

        /// <summary>
        /// Convert the resource configuration
        /// </summary>
        private DataQualityResourceConfiguration ConvertToResourceConfiguration(DataContext ctx, DbDataQualityResourceConfiguration resourceConfiguration) => new DataQualityResourceConfiguration()
        {
            ResourceName = resourceConfiguration.ResourceName,
            Assertions = ctx.Query<DbDataQualityResourceAssertion>(a => a.DataQualityResourceKey == resourceConfiguration.Key).ToArray().Select(a => new DataQualityResourceAssertion()
            {
                Evaluation = a.Evaluation,
                Expressions = a.Expressions.Split(';').ToList(),
                Id = a.Id,
                Text = a.Text,
                Priority = a.Priority,
            }).ToList()
        };

        /// <inheritdoc/>
        public IEnumerable<DataQualityRulesetConfiguration> GetRuleSets(bool includeObsolete = false)
        {
            try
            {
                using (var ctx = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    ctx.Open();
                    Expression<Func<DbDataQualityConfiguration, Boolean>> qry = o => o.ObsoletionTime == null;
                    if(includeObsolete)
                    {
                        qry = o => true;
                    }

                    return ctx.Query<DbDataQualityConfiguration>(qry).ToArray().Select(o => this.ConvertToRuleSet(ctx, o)).ToArray();
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATAQUALITY_CONFIG_READ_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<DataQualityResourceConfiguration> GetRulesForType<T>() => this.GetRulesForType(typeof(T));


        /// <inheritdoc/>
        public IEnumerable<DataQualityResourceConfiguration> GetRulesForType(Type forType) { 
            DataQualityResourceConfiguration[] retVal = null;
            var serializationName = forType.GetSerializationName();

            if (this.m_adhocCache?.TryGet<DataQualityResourceConfiguration[]>($"dq.res.{serializationName}", out retVal) == true)
            {
                return retVal;
            }
            try
            {
                using (var ctx = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    ctx.Open();

                    var stmt = ctx.CreateSqlStatementBuilder().SelectFrom(typeof(DbDataQualityResourceConfiguration), typeof(DbDataQualityConfiguration))
                        .InnerJoin<DbDataQualityResourceConfiguration, DbDataQualityConfiguration>(o => o.DataQualityConfigurationKey, o => o.Key)
                        .Where<DbDataQualityConfiguration>(o => o.Enabled && o.ObsoletionTime == null)
                        .And<DbDataQualityResourceConfiguration>(o => o.ResourceName == serializationName);

                    retVal = ctx.Query<DbDataQualityResourceConfiguration>(stmt.Statement).ToArray()
                        .Select(o => this.ConvertToResourceConfiguration(ctx, o)).ToArray();
                    this.m_adhocCache?.Add($"dq.res.{serializationName}", retVal);
                    return retVal;
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATAQUALITY_CONFIG_READ_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public DataQualityRulesetConfiguration SaveRuleSet(DataQualityRulesetConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            bool isSystemContext = AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal;
            if (!isSystemContext)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterSystemConfiguration);
            }

            try
            {
                using (var ctx = this.m_configuration.Provider.GetWriteConnection())
                {
                    ctx.Open();
                    using (var tx = ctx.BeginTransaction())
                    {
                        this.m_adhocCache?.Remove(this.CreateCacheKey(configuration.Id));

                        var existingConfiguration = ctx.FirstOrDefault<DbDataQualityConfiguration>(o => o.Id == configuration.Id);
                        if (existingConfiguration != null)
                        {
                            existingConfiguration.ObsoletionTime = null;
                            existingConfiguration.ObsoletedByKey = null;
                            existingConfiguration.ObsoletedByKeySpecified = existingConfiguration.ObsoletionTimeSpecified = true;
                            existingConfiguration.UpdatedTime = DateTimeOffset.Now;
                            existingConfiguration.UpdatedByKey = ctx.EstablishProvenance(AuthenticationContext.Current.Principal);
                            existingConfiguration.Enabled = configuration.Enabled;
                            existingConfiguration.Name = configuration.Name;
                            ctx.Update(existingConfiguration);
                        }
                        else
                        {
                            existingConfiguration = ctx.Insert(new DbDataQualityConfiguration()
                            {
                                CreatedByKey = ctx.EstablishProvenance(AuthenticationContext.Current.Principal),
                                CreationTime = DateTimeOffset.Now,
                                Enabled = configuration.Enabled,
                                Id = configuration.Id,
                                Name = configuration.Name,
                                Key = configuration.Key ?? Guid.NewGuid()
                            });
                        }

                        // Save the resource configurations
                        var resourceConfigIds = ctx.Query<DbDataQualityResourceConfiguration>(o => o.DataQualityConfigurationKey == existingConfiguration.Key).Select(o => o.Key).ToArray();
                        ctx.DeleteAll<DbDataQualityResourceAssertion>(r => resourceConfigIds.Contains(r.DataQualityResourceKey));
                        ctx.DeleteAll<DbDataQualityResourceConfiguration>(r => resourceConfigIds.Contains(r.Key));

                        foreach (var config in configuration.Resources)
                        {
                            var dbConfiguration = ctx.Insert(new DbDataQualityResourceConfiguration()
                            {
                                DataQualityConfigurationKey = existingConfiguration.Key,
                                ResourceName = config.ResourceName
                            });
                            ctx.InsertAll(config.Assertions.Select(o => new DbDataQualityResourceAssertion()
                            {
                                DataQualityResourceKey = dbConfiguration.Key,
                                Evaluation = o.Evaluation,
                                Expressions = string.Join(";", o.Expressions),
                                Id = o.Id,
                                Priority = o.Priority,
                                Text = o.Text
                            }));
                        }


                        var retVal = this.ConvertToRuleSet(ctx, existingConfiguration);
                        tx.Commit();
                        this.m_adhocCache?.Add(this.CreateCacheKey(configuration.Id), retVal);
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
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATAQUALITY_CONFIG_WRITE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public void RemoveRuleSet(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterSystemConfiguration);

            try
            {
                using (var ctx = this.m_configuration.Provider.GetWriteConnection())
                {
                    ctx.Open();

                    var existing = ctx.FirstOrDefault<DbDataQualityConfiguration>(o => o.Id == id);
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(id);
                    }
                    else if (existing.ObsoletionTime == null) // set obsoletion (soft delete)
                    {
                        existing.ObsoletionTime = DateTimeOffset.Now;
                        existing.ObsoletedByKey = ctx.EstablishProvenance(AuthenticationContext.Current.Principal);
                        ctx.Update(existing);
                    }
                    else // hard delete
                    {
                        using (var tx = ctx.BeginTransaction())
                        {
                            var resourceConfigIds = ctx.Query<DbDataQualityResourceConfiguration>(o => o.DataQualityConfigurationKey == existing.Key).Select(o => o.Key).ToArray();
                            ctx.DeleteAll<DbDataQualityResourceAssertion>(r => resourceConfigIds.Contains(r.DataQualityResourceKey));
                            ctx.DeleteAll<DbDataQualityResourceConfiguration>(r => resourceConfigIds.Contains(r.Key));
                            ctx.Delete(existing);
                            tx.Commit();
                        }
                    }

                    this.m_adhocCache?.Remove(this.CreateCacheKey(id));
                    this.m_adhocCache?.RemoveAll("dq\\.res\\..*");
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATAQUALITY_CONFIG_WRITE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {

            // Get all logically deleted past cutoff
            foreach (var configId in context.Query<DbDataQualityConfiguration>(o => o.ObsoletionTime != null && o.ObsoletionTime < deletedCutoff).Select(o => o.Key).ToArray())
            {
                var resourceConfigIds = context.Query<DbDataQualityResourceConfiguration>(o => o.DataQualityConfigurationKey == configId).Select(o => o.Key).ToArray();
                context.DeleteAll<DbDataQualityResourceAssertion>(r => resourceConfigIds.Contains(r.DataQualityResourceKey));
                context.DeleteAll<DbDataQualityResourceConfiguration>(r => resourceConfigIds.Contains(r.Key));
                context.DeleteAll<DbDataQualityConfiguration>(o => o.Key == configId);
            }
        }
    }
}
