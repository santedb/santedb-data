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
