﻿/*
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
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Jobs;
using SanteDB.Persistence.Data.Services.Persistence;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// A daemon service which registers the other persistence services
    /// </summary>
    [ServiceProvider("ADO.NET Persistence Service", Configuration = typeof(AdoPersistenceConfigurationSection))]
    public class AdoPersistenceService : ISqlDataPersistenceService, IServiceFactory, IReportProgressChanged, IProvideBackupAssets, IDisposable
    {
        
        // Service factory types
        private readonly Type[] m_serviceFactoryTypes = new Type[]
        {
            typeof(AdoForeignDataManager),
            typeof(AdoRelationshipValidationProvider),
            typeof(AdoFreetextSearchService),
            typeof(AdoDatasetInstallerService),
            typeof(AdoBiDatamartRepository),
            typeof(AdoJobManager),
            typeof(OrmBiDataProvider),
            typeof(OrmBiDataIntegrator),
            typeof(AdoRecordMatchingConfigurationService),
            typeof(AdoRelationshipValidationProvider),
            typeof(AdoDataSigningCertificateManagerService),
            typeof(AdoCertificateIdentityProvider),
            typeof(AdoCdssLibraryRepository),
            typeof(AdoDataQualityConfigurationProvider),
            typeof(AdoDataTemplateManager)
        };

        // Gets the configuration
        private AdoPersistenceConfigurationSection m_configuration;

        // Trace source for the service
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoPersistenceService));

        // Mapper for this service
        private ModelMapper m_mapper;

        // Service manager
        private readonly IServiceManager m_serviceManager;

        // Service list
        private IList<IAdoPersistenceProvider> m_services = new List<IAdoPersistenceProvider>();

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// ADO Persistence service
        /// </summary>
        public AdoPersistenceService(IConfigurationManager configManager, 
            IServiceManager serviceManager, 
            IJobManagerService jobManager, 
            IBiMetadataRepository biMetadataRepository = null)
        {
            try
            {
                this.m_configuration = configManager.GetSection<AdoPersistenceConfigurationSection>();
                this.m_mapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
                this.m_serviceManager = serviceManager;

                QueryBuilder.AddQueryHacks(serviceManager.CreateAll<IQueryBuilderHack>(this.m_mapper));


                // Upgrade the schema
                this.m_configuration.Provider.UpgradeSchema("SanteDB.Persistence.Data", serviceManager.NotifyStartupProgress);

                // Iterate and register ADO data persistence services
                foreach (var pservice in serviceManager.CreateInjectedOfAll<IAdoPersistenceProvider>())
                {
                    pservice.Provider = this.m_configuration.Provider;
                    serviceManager.AddServiceProvider(pservice);
                    this.m_services.Add(pservice);
                    if (pservice is IReportProgressChanged irpc) // Cascade these status updates
                    {
                        irpc.ProgressChanged += (o, e) => this.ProgressChanged?.Invoke(o, e);
                    }
                }
                serviceManager.AddServiceProvider(typeof(TagPersistenceService));
                serviceManager.AddServiceProvider(typeof(AdoRelationshipValidationProvider));
                serviceManager.AddServiceProvider(typeof(AdoDatasetInstallerService));

                if (jobManager.GetJobInstance(OptimizeDatabaseJob.ID) == null)
                {
                    var job = serviceManager.CreateInjected<OptimizeDatabaseJob>();
                    jobManager.AddJob(job, JobStartType.DelayStart);
                    jobManager.SetJobSchedule(job, new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }, DateTime.Now.Date.AddHours(3));
                }


                // Add this to the BI layer
                using (AuthenticationContext.EnterSystemContext())
                {
                    // Add audits as a BI data source
                    biMetadataRepository?.Insert(new BiDataSourceDefinition()
                    {
                        IsSystemObject = true,
                        ConnectionString = this.m_configuration.ReadonlyConnectionString,
                        Status = BiDefinitionStatus.Active,
                        MetaData = new BiMetadata()
                        {
                            Version = typeof(AdoPersistenceService).Assembly.GetName().Version.ToString(),
                            Demands = new List<string>()
                                {
                                PermissionPolicyIdentifiers.Login
                                }
                        },
                        Id = "org.santedb.bi.dataSource.main",
                        Name = "main",
                        ProviderType = typeof(OrmBiDataProvider)
                    });

                    this.DisableConstraintsForGateway();

                }
                ;
            }
            catch (ModelMapValidationException e)
            {
                this.m_tracer.TraceError("Error validing map: {0}", e.Message);
                foreach (var i in e.ValidationDetails)
                {
                    this.m_tracer.TraceError("{0}:{1} @ {2}", i.Level, i.Message, i.Location);
                }

                throw;
            }
        }

        /// <summary>
        /// HACK for DCDR on POSTGRES: Disable constraints for gateway
        /// </summary>
        private void DisableConstraintsForGateway()
        {
            this.m_tracer.TraceInfo("Disabling Constraints in Database");

            if (this.m_configuration.Provider is IDisableConstraintProvider idcp && ApplicationServiceContext.Current.HostType == SanteDBHostType.Gateway)
            {
                try
                {
                    using (var dbc = this.m_configuration.Provider.GetWriteConnection())
                    {
                        dbc.Open();
                        idcp.DisableAllConstraints(dbc);
                    }
                }
                catch(Exception e)
                {
                    this.m_tracer.TraceWarning("WARNING: Could not disable constraints - synchronization may be impacted");
                }
            }
        }

        /// <summary>
        /// HACK for DCDR on POSTGRES: Enable all constraints on the gateway
        /// </summary>
        private void EnableConstraintsForGateway()
        {
            this.m_tracer.TraceInfo("Re-Enabling Constraints in Database");
            if (this.m_configuration.Provider is IDisableConstraintProvider idcp && ApplicationServiceContext.Current.HostType == SanteDBHostType.Gateway)
            {
                try
                {
                    using (var dbc = this.m_configuration.Provider.GetWriteConnection())
                    {
                        dbc.Open();
                        idcp.EnableAllConstraints(dbc);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceWarning("WARNING: Could not enable constraints - data integrity may be impacted");
                }
            }
        }

        /// <summary>
        /// Gets the invariant name of this service
        /// </summary>
        public string InvariantName => this.m_configuration.Provider.Invariant;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO Persistence Service";

        /// <summary>
        /// Execute a non-query SQL script
        /// </summary>
        public void ExecuteNonQuery(string sql)
        {
            if (String.IsNullOrEmpty(sql))
            {
                throw new ArgumentNullException(nameof(sql));
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {
                        context.ExecuteNonQuery(sql);
                        tx.Commit();
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error executing SQL statement {0} - {1}", sql, e.Message);
                    throw new DataPersistenceException("Error executing raw SQL", e);
                }
            }
        }

        /// <summary>
        /// Try to create the specified service
        /// </summary>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if (this.TryCreateService(typeof(TService), out var strongInstance))
            {
                serviceInstance = (TService)strongInstance;
                return true;
            }
            serviceInstance = default(TService);
            return false;
        }

        /// <summary>
        /// Try to create the specified service
        /// </summary>
        public bool TryCreateService(Type serviceType, out object serviceInstance)
        {
            var knownServiceType = this.m_serviceFactoryTypes.FirstOrDefault(o => serviceType.IsAssignableFrom(o));
            if (knownServiceType != null)
            {
                serviceInstance = this.m_serviceManager.CreateInjected(knownServiceType);
                return true;
            }
            serviceInstance = this.m_services.FirstOrDefault(o => serviceType.IsAssignableFrom(o.GetType()));
            return serviceInstance != null;
        }


        /// <inheritdoc/>
        public IEnumerable<IBackupAsset> GetBackupAssets()
        {
            var retVal = this.m_configuration.Provider.CreateBackupAsset(DataConstants.PRIMARY_DATABASE_ASSET_ID);
            if (retVal != null)
            {
                yield return retVal;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.EnableConstraintsForGateway();
            if(this.m_configuration.Provider is IDisposable dispose)
            {
                dispose.Dispose();
            }
        }
    }
}