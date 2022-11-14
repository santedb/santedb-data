using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Features;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Synchronization.ADO.Configuration
{
    /// <summary>
    /// 
    /// </summary>
    public class AdoSynchronizationFeature : GenericServiceFeature<AdoSynchronizationRepositoryService>
    {
        /// <inheritdoc />
        public override Type ConfigurationType => typeof(AdoSynchronizationConfigurationSection);

        /// <inheritdoc />
        public override string Group => FeatureGroup.Persistence;

        /// <inheritdoc />
        protected override object GetDefaultConfiguration() => new AdoSynchronizationConfigurationSection
        {
            TraceSql = false
        };

        /// <inheritdoc />
        public override FeatureFlags Flags => FeatureFlags.AutoSetup;
    }
}
