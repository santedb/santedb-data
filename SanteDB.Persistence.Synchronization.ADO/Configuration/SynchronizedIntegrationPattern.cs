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
using SanteDB.BI.Services.Impl;
using SanteDB.Cdss.Xml;
using SanteDB.Client.Configuration;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Disconnected.Services;
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Data;
using SanteDB.Core.Data.Management;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Matcher.Matchers;
using SanteDB.Matcher.Services;
using SanteDB.Persistence.Auditing.ADO.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Services;
using SanteDB.Persistence.PubSub.ADO;
using SanteDB.Persistence.Synchronization.ADO.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.Persistence.Synchronization.ADO.Configuration
{
    /// <summary>
    /// Synchronized upstream integration pattern
    /// </summary>
    public class SynchronizedIntegrationPattern : IUpstreamIntegrationPattern
    {
        /// <inheritdoc/>
        public string Name => "synchronize";

        /// <inheritdoc/>
        public IEnumerable<Type> GetServices() => new Type[]
                    {
                        typeof(ClientPolicyDecisionProviderService),
                        typeof(DefaultTfaService),
                        typeof(BridgedSessionManager),
                        typeof(AdoSessionProvider),
                        typeof(AdoPersistenceService),
                        typeof(UpstreamSynchronizationService),
                        typeof(AdoIdentityProvider),
                        typeof(AdoRoleProvider),
                        typeof(AdoDeviceIdentityProvider),
                        typeof(AdoApplicationIdentityProvider),
                        typeof(AppletClinicalProtocolInstaller),
                        typeof(AdoSecurityChallengeProvider),
                        typeof(AdoCertificateIdentityProvider),
                        typeof(AdoFreetextSearchService),
                        typeof(BridgedRoleProvider),
                        typeof(BridgedApplicationIdentityProvider),
                        typeof(AdoPolicyInformationService),
                        typeof(FileSystemDataQualityConfigurationProvider),
                        typeof(FileSystemCdssLibraryRepository),
                        typeof(FileSystemDataTemplateManager),
                        typeof(AdoRelationshipValidationProvider),
                        typeof(AdoAuditRepositoryService),
                        typeof(AdoPubSubManager),
                        typeof(BridgedSecurityRepositoryService),
                        typeof(UpstreamSecurityRepository),
                        typeof(UpstreamDataTemplateManagementService),
                        typeof(BridgedIdentityProvider),
                        typeof(UpstreamIdentityProvider),
                        typeof(UpstreamApplicationIdentityProvider),
                        typeof(BridgedPolicyInformationService),
                        typeof(UpstreamPolicyInformationService),
                        typeof(UpstreamRoleProviderService),
                        typeof(LocalSecurityRepositoryService),
                        typeof(SynchronizationAuditDispatcher),
                        typeof(UpstreamSecurityChallengeProvider),
                        typeof(WeightedRecordMatchingService),
                        typeof(FileMatchConfigurationProvider),
                        typeof(PersistenceEntitySource),
                        typeof(AdoSynchronizationManager),
                        typeof(LocalRepositoryFactory),
                        typeof(DefaultDatamartManager),
                        typeof(LocalBiRenderService),
                        typeof(InMemoryPivotProvider),
                        typeof(AppletBiRepository),
                        typeof(AdoSubscriptionExecutor),
                    };

        /// <summary>
        /// Set default configurations
        /// </summary>
        public void SetDefaults(SanteDBConfiguration configuration)
        {
            var adoConfiguration = configuration.GetSection<AdoPersistenceConfigurationSection>();

            // Versioning policy default 
            if (ApplicationServiceContext.Current.HostType != SanteDBHostType.Gateway &&
                ApplicationServiceContext.Current.HostType != SanteDBHostType.Server) // Client configuration should not version data
            {

                adoConfiguration.VersioningPolicy = AdoVersioningPolicyFlags.None;
                adoConfiguration.TrimSettings = new AdoTrimSettings()
                {
                    MaxDeletedDataRetention = new TimeSpan(30, 0, 0),
                    MaxSessionRetention = new TimeSpan(30, 0, 0)
                };
                adoConfiguration.Validation = new List<AdoValidationPolicy>() {
                    new AdoValidationPolicy() {
                        Uniqueness = AdoValidationEnforcement.Loose,
                        Target = new ResourceTypeReferenceConfiguration(typeof(Entity)),
                        Authority = AdoValidationEnforcement.Strict,
                        Format = AdoValidationEnforcement.Strict,
                        CheckDigit = AdoValidationEnforcement.Strict,
                        Scope = AdoValidationEnforcement.Strict
                    }
                };
                adoConfiguration.AutoInsertChildren = true;
                adoConfiguration.AutoUpdateExisting = true;
                adoConfiguration.DeleteStrategy = DeleteMode.PermanentDelete;
                adoConfiguration.LoadStrategy = LoadMode.FullLoad;
                adoConfiguration.MaxPageSize = 25;

            }

        }
    }
}
