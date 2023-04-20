using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Matching;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Matcher.Definition;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Record matching persistence service using the database
    /// </summary>
    public class AdoRecordMatchingConfigurationService : IRecordMatchingConfigurationService
    {
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;
        private readonly IAdhocCacheService m_adhocCacheService;
        private readonly IPolicyEnforcementService m_pepService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoRecordMatchingConfigurationService(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, ILocalizationService localizationService, IAdhocCacheService cacheService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_adhocCacheService = cacheService;
            this.m_pepService = pepService;
        }

        /// <inheritdoc/>
        public IEnumerable<IRecordMatchingConfiguration> Configurations
        {
            get
            {
                try
                {
                    using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                    {
                        context.Open();

                        var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbMatchConfiguration), typeof(DbMatchConfigurationVersion))
                            .InnerJoin<DbMatchConfiguration, DbMatchConfigurationVersion>(o => o.Key, o => o.Key)
                            .Where<DbMatchConfigurationVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                            .Statement.Prepare();
                        return context.Query<CompositeResult<DbMatchConfiguration, DbMatchConfigurationVersion>>(stmt).AsEnumerable()
                                .Select(itm =>
                        {
                            MatchConfiguration matchConfig = null;
                            if (this.m_adhocCacheService?.TryGet($"matcher.config.{itm.Object1.Id}", out matchConfig) != true)
                            {
                                using (var ms = new MemoryStream(itm.Object2.Definition))
                                {
                                    matchConfig = MatchConfiguration.Load(ms);
                                }
                                this.m_adhocCacheService?.Add($"matcher.config.{itm.Object1.Id}", matchConfig);
                            }
                            return matchConfig;
                        }).ToList();
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Match Configuration Provider";

        /// <inheritdoc/>
        public IRecordMatchingConfiguration DeleteConfiguration(string configurationId)
        {
            if (String.IsNullOrEmpty(configurationId))
            {
                throw new ArgumentNullException(nameof(configurationId));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.UnrestrictedMatchConfiguration);

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    var config = context.FirstOrDefault<DbMatchConfiguration>(o => o.Id == configurationId);
                    if (config == null)
                    {
                        throw new KeyNotFoundException(configurationId);
                    }

                    var currentVersion = context.FirstOrDefault<DbMatchConfigurationVersion>(o => o.Key == config.Key && o.IsHeadVersion);
                    currentVersion.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                    currentVersion.ObsoletionTime = DateTimeOffset.Now;
                    context.Update(currentVersion);

                    // Load the current version
                    this.m_adhocCacheService?.Remove($"matcher.config.{configurationId}");
                    using (var ms = new MemoryStream(currentVersion.Definition))
                    {
                        return MatchConfiguration.Load(ms);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.MATCH_CONFIG_ERR, new { id = configurationId }), e);
            }
        }

        /// <inheritdoc/>
        public IRecordMatchingConfiguration GetConfiguration(string configurationId)
        {
            if (String.IsNullOrEmpty(configurationId))
            {
                throw new ArgumentNullException(nameof(configurationId));
            }

            MatchConfiguration retVal = null;
            if (this.m_adhocCacheService?.TryGet($"matcher.config.{configurationId}", out retVal) != true)
            {
                try
                {
                    using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                    {
                        context.Open();

                        var stmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbMatchConfiguration), typeof(DbMatchConfigurationVersion))
                          .InnerJoin<DbMatchConfiguration, DbMatchConfigurationVersion>(o => o.Key, o => o.Key)
                          .Where<DbMatchConfigurationVersion>(o => o.IsHeadVersion && o.ObsoletionTime == null)
                          .Statement.Prepare();
                        var queryResult = context.FirstOrDefault<CompositeResult<DbMatchConfiguration, DbMatchConfigurationVersion>>(stmt);
                        if (queryResult == null)
                        {
                            return null;
                        }
                        using (var ms = new MemoryStream(queryResult.Object2.Definition))
                        {
                            retVal = MatchConfiguration.Load(ms);
                        }
                        this.m_adhocCacheService?.Add($"matcher.config.{configurationId}", retVal);
                    }
                }
                catch (DbException e)
                {
                    throw e.TranslateDbException();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.MATCH_CONFIG_ERR, new { id = configurationId }), e);
                }
            }
            return retVal;
        }

        /// <inheritdoc/>
        public IRecordMatchingConfiguration SaveConfiguration(IRecordMatchingConfiguration configuration)
        {

            if(configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterMatchConfiguration);

            try
            {
                using(var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    using(var tx = context.BeginTransaction())
                    {
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        // First - check if we already have a root
                        if(configuration is MatchConfiguration mcc)
                        {
                            mcc = this.SaveInternal(context, mcc);
                            tx.Commit();
                            this.m_adhocCacheService?.Add($"matcher.config.{configuration.Id}", mcc);

                            return mcc;
                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(MatchConfiguration), configuration.GetType()));
                        }
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.MATCH_CONFIG_ERR, new { id = configuration.Id }));
            }
        }

        /// <summary>
        /// Save the match configuration to the data context
        /// </summary>
        private MatchConfiguration SaveInternal(DataContext context, MatchConfiguration configuration)
        {
            var existingRoot = context.FirstOrDefault<DbMatchConfiguration>(o => o.Id == configuration.Id);
            if(existingRoot == null)
            {
                existingRoot = context.Insert(new DbMatchConfiguration()
                {
                    Id = configuration.Id,
                    Key = configuration.Uuid
                });
                configuration.Uuid = existingRoot.Key;
            }

            // Existing version?
            var currentVersion = context.Query<DbMatchConfigurationVersion>(o => o.IsHeadVersion).OrderByDescending(o => o.VersionSequenceId).FirstOrDefault();
            var newVersion = new DbMatchConfigurationVersion();
            if(currentVersion != null)
            {
                newVersion = new DbMatchConfigurationVersion().CopyObjectData(currentVersion);
                newVersion.ReplacesVersionKey = currentVersion.VersionKey;
                newVersion.VersionKey = Guid.Empty;
                newVersion.VersionSequenceId = null;
                currentVersion.ObsoletionTime = DateTimeOffset.Now;
                currentVersion.ObsoletedByKey = context.ContextId;
                newVersion.ObsoletionTime = null;
                newVersion.ObsoletedByKey = null;
                currentVersion.IsHeadVersion = false;
                
                newVersion.ObsoletedByKeySpecified = newVersion.ObsoletionTimeSpecified = true;
                context.Update(currentVersion);
            }
            else
            {
                configuration.Metadata = configuration.Metadata ?? new MatchConfigurationMetadata();
                configuration.Metadata.CreatedBy = configuration.Metadata.CreatedBy ?? AuthenticationContext.Current.Principal.Identity.Name;
                configuration.Metadata.CreationTime = DateTime.Now;
            }

            newVersion.CreatedByKey = context.ContextId;
            newVersion.CreationTime = DateTimeOffset.Now;
            //newVersion.ReplacesVersionKey = currentVersion?.VersionKey;
            newVersion.Key = existingRoot.Key;
            newVersion.AppliesToType = new TypeReferenceConfiguration(configuration.AppliesTo.First()).TypeXml;
            newVersion.Status = configuration.Metadata?.Status ?? MatchConfigurationStatus.Inactive;
            configuration.Metadata.Version += 1;
            using (var ms = new MemoryStream()) {
                configuration.Save(ms);
                newVersion.Definition = ms.ToArray();
            }
            newVersion.IsHeadVersion = true;

            context.Insert(newVersion);
            return configuration;
        }

    }
}
