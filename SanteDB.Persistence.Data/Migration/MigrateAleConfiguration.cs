using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Migration;
using SanteDB.Persistence.Data.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Migration
{
    /// <summary>
    /// SQL Feature initializer
    /// </summary>
    public class MigrateAleConfiguration : ISqlFeatureInitializer
    {
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(MigrateAleConfiguration));

        /// <summary>
        /// DI Constructor
        /// </summary>
        public MigrateAleConfiguration(IConfigurationManager configurationManager)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
        }

        /// <inheritdoc/>
        public void AfterInstall(DataContext context)
        {
            if(this.m_configuration.AleConfiguration?.AleEnabled == true)
            {
                this.m_tracer.TraceInfo("Migrating ALE settings for SMK");
                context.ExecuteNonQuery("UPDATE ALE_SYSTBL SET X5T = ?", this.m_configuration.AleConfiguration.Certificate.Certificate.Thumbprint);
            }
        }

        /// <inheritdoc/>
        public bool BeforeInstall(DataContext context) => true;
    }
}
