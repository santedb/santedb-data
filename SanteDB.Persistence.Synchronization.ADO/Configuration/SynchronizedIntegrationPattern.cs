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
using SanteDB.Client.Configuration;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Repositories;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core.Data;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Persistence.Data.Services;
using System;
using System.Collections.Generic;
using System.Text;

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
                        typeof(AdoPersistenceService),
                        typeof(UpstreamSynchronizationService),
                        typeof(AdoIdentityProvider),
                        typeof(AdoRoleProvider),
                        typeof(AdoDeviceIdentityProvider),
                        typeof(AdoApplicationIdentityProvider),
                        typeof(AdoSecurityChallengeProvider),
                        typeof(AdoCertificateIdentityProvider),
                        typeof(AdoFreetextSearchService),
                        typeof(AdoPolicyInformationService),
                        typeof(AdoRelationshipValidationProvider),
                        typeof(UpstreamIdentityProvider),
                        typeof(UpstreamApplicationIdentityProvider),
                        typeof(UpstreamPolicyInformationService),
                        typeof(UpstreamRoleProviderService),
                        typeof(UpstreamSecurityRepository),
                        typeof(UpstreamSecurityChallengeProvider),
                        typeof(PersistenceEntitySource),
                        typeof(AdoSynchronizationRepositoryService),
                        typeof(DefaultSynchronizationQueueManager),
                        typeof(LocalRepositoryFactory)
                    };
    }
}
