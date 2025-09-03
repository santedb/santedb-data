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
using SanteDB.Core;
using SanteDB.Core.Data.Initialization;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// A <see cref="IDatasetInstallerService"/> which can install datasets in ADO.NET persistence layer
    /// </summary>
    [ServiceProvider("ADO.NET Dataset Installer", Configuration = typeof(AdoPersistenceConfigurationSection))]
    public class AdoDatasetInstallerService : IDatasetInstallerService, IReportProgressChanged
    {

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoDatasetInstallerService));

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoDatasetInstallerService(IConfigurationManager configurationManager)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Dataset Installation Service";

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<Dataset>> Installed;


        /// <summary>
        /// Re-organize a bundle's contents for insert
        /// </summary>
        public Dataset ReorganizeForInsert(Dataset input)
        {
            var resolved = input.Action.ToArray();

            // Process each object in our queue of to be processed
            bool swapped = true;
            for (var outer = 0; outer < resolved.Length && swapped; outer++)
            {
                swapped = false;
                for (var inner = 0; inner < resolved.Length; inner++)
                {
                    var itm = resolved[inner];
                    IEnumerable<int> dependencies = new int[0];
                    switch (itm.Element)
                    {
                        case Entity entity:
                            dependencies = dependencies.Concat(entity.Relationships?.Select(r => Array.FindIndex(resolved, k => k.Element.Key == r.TargetEntityKey)) ?? new int[0]);
                            dependencies = dependencies.Concat(entity.Participations?.Select(p => Array.FindIndex(resolved, i => i.Element.Key == p.ActKey)) ?? new int[0]);
                            break;
                        case Act act:
                            dependencies = dependencies.Concat(act.Relationships?.Select(r => Array.FindIndex(resolved, i => i.Element.Key == r.TargetActKey)) ?? new int[0]);
                            dependencies = dependencies.Concat(act.Participations?.Select(p => Array.FindIndex(resolved, i => i.Element.Key == p.PlayerEntityKey)) ?? new int[0]);
                            break;
                        case Concept concept:
                            dependencies = dependencies.Concat(concept.Relationships?.Select(r => Array.FindIndex(resolved, i => i.Element.Key == r.TargetConceptKey)) ?? new int[0]);
                            dependencies = dependencies.Concat(concept.ConceptSetsXml?.Select(o => Array.FindIndex(resolved, i => i.Element.Key == o)) ?? new int[0]);
                            break;
                        case ITargetedAssociation ta:
                            dependencies = new int[] {
                                    Array.FindIndex(resolved, i => i.Element.Key == ta.TargetEntityKey), 
                                Array.FindIndex(resolved, i => i.Element.Key == ta.SourceEntityKey) 
                            };
                            break;

                    }

                    // Scan dependencies and swap
                    var index = inner;
                    var scanSwap = dependencies.ToArray();
                    foreach (var dep in scanSwap)
                    {
                        if (dep > index)
                        {
                            swapped = true;
                            resolved[index] = resolved[dep];
                            resolved[dep] = itm;
                            index = dep;
                        }
                    }
                }
            }

            // Could not order dependencies
            //if (swapped)
            //{
            //    throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_CIRCULAR_DEPENDENCY));
            //}

            return new Dataset(input.Id) { Action = resolved.ToList(), ServiceExec = input.ServiceExec, SqlExec = input.SqlExec, PreSqlExec = input.PreSqlExec };
        }

        /// <summary>
        /// Get the installation date
        /// </summary>
        public DateTimeOffset? GetInstallDate(string dataSetId)
        {
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    var patchId = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(dataSetId)).HexEncode();

                    context.Open(initializeExtensions: false);
                    return context.Query<DbPatch>(o => o.PatchId == patchId).Select(o => o.ApplyDate).FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(String.Empty, e);
                }
            }
        }

        /// <summary>
        /// Get all installed datasets
        /// </summary>
        public IEnumerable<string> GetInstalled()
        {
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);
                    return context.Query<DbPatch>(o => true).Select(o => o.PatchId);
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(String.Empty, e);
                }
            }
        }

        /// <summary>
        /// Install the dataset into the primary datastore
        /// </summary>
        /// <param name="dataset">The dataset to be installed</param>
        /// <returns>True if installation succeeded</returns>
        public bool Install(Dataset dataset)
        {
            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset), ErrorMessages.ARGUMENT_NULL);
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);
                    context.AddOrUpdateData(DataConstants.DisableObjectValidation, true);

                    var patchId = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(dataset.Id)).HexEncode();

                    // Check if dataset is installed
                    if (context.Any<DbPatch>(o => o.PatchId == patchId))
                    {
                        this.m_tracer.TraceVerbose("Skipping {0} because it is already installed", dataset.Id);
                        return false;
                    }
                    this.m_tracer.TraceInfo("Installing dataset {0}...", dataset.Id);

                    using (var tx = context.BeginTransaction())
                    {


                        context.ContextId = context.EstablishProvenance(AuthenticationContext.Current.Principal, null);

                        // Insert the post install trigger
                        if (dataset.PreSqlExec?.Any() == true)
                        {
                            this.m_tracer.TraceInfo("Executing post-install triggers for {0}...", dataset.Id);
                            foreach (var itm in dataset.PreSqlExec.Where(o => o.InvariantName == this.m_configuration.Provider.Invariant))
                            {
                                context.ExecuteNonQuery(itm.QueryText);
                            }
                        }

                        dataset = this.ReorganizeForInsert(dataset);

                        for (var i = 0; i < dataset.Action.Count; i++)
                        {
                            var itm = dataset.Action[i];
                            // Clear the provenance times
                            if (itm.Element is BaseEntityData be)
                            {
                                be.CreationTime = default(DateTimeOffset);
                                be.CreatedByKey = null;
                                be.ObsoletionTime = null;
                                be.ObsoletedByKey = null;
                            }
                            if (itm.Element is NonVersionedEntityData nve)
                            {
                                nve.CreationTime = default(DateTimeOffset);
                                nve.CreatedByKey = null;
                                nve.ObsoletionTime = null;
                                nve.ObsoletedByKey = null;
                                nve.UpdatedTime = null;
                                nve.UpdatedByKey = null;
                            }
                            this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(AdoDatasetInstallerService), (float)i / (float)dataset.Action.Count, String.Format(UserMessages.PROCESSING, dataset.Id)));
                            var persistenceService = itm.Element.GetType().GetRelatedPersistenceService();
                            this.m_tracer.TraceVerbose("{0} {1}", itm.ActionName, itm.Element);
                            try
                            {
                                switch (itm)
                                {
                                    case DataInsert di:
                                        if (di.SkipIfExists && persistenceService.Exists(context, itm.Element.Key.GetValueOrDefault()))
                                        {
                                            continue;
                                        }

                                        persistenceService.Insert(context, di.Element);
                                        break;
                                    case DataUpdate du:
                                        if (du.InsertIfNotExists && !persistenceService.Exists(context, itm.Element.Key.GetValueOrDefault()))
                                        {
                                            persistenceService.Insert(context, itm.Element);
                                        }
                                        else
                                        {
                                            persistenceService.Update(context, itm.Element);
                                        }

                                        break;
                                    case DataDelete dd:
                                        if (persistenceService.Exists(context, dd.Element.Key.Value))
                                        {
                                            persistenceService.Delete(context, dd.Element.Key.Value, DataPersistenceControlContext.Current?.DeleteMode ?? this.m_configuration.DeleteStrategy);
                                        }
                                        break;
                                }
                            }
                            catch (DbException e)
                            {

                                this.m_tracer.TraceError("Installing {0} (#{1} in dataset) failed", itm, i);
                                throw e.TranslateDbException();
                            }
                            catch (Exception e)
                            {
                                if (itm.IgnoreErrors)
                                {
                                    this.m_tracer.TraceWarning("Error applying dataset - ignore errors is enabled - {0}", e.Message);
                                    return false;
                                }
                                else
                                {
                                    this.m_tracer.TraceError("Error applying dataset item {0} - {1}", i, e.ToHumanReadableString());
                                    throw;
                                }
                            }
                        }

                        // Insert the post install trigger
                        if (dataset.SqlExec?.Any() == true)
                        {
                            this.m_tracer.TraceInfo("Executing post-install triggers for {0}...", dataset.Id);
                            foreach (var itm in dataset.SqlExec.Where(o => o.InvariantName == this.m_configuration.Provider.Invariant))
                            {
                                context.ExecuteNonQuery(itm.QueryText);
                            }
                        }

                        if (dataset.ServiceExec?.Any() == true)
                        {
                            this.m_tracer.TraceInfo("Executing post-install service actions for {0}...", dataset.Id);
                            foreach (var svc in dataset.ServiceExec)
                            {
                                var serviceType = Type.GetType(svc.ServiceType);
                                if (serviceType == null)
                                {
                                    throw new InvalidOperationException(String.Format(ErrorMessages.TYPE_NOT_FOUND, svc.ServiceType));
                                }
                                var serviceInstance = ApplicationServiceContext.Current.GetService(serviceType);
                                if (serviceInstance == null)
                                {
                                    throw new InvalidOperationException(String.Format(ErrorMessages.SERVICE_NOT_FOUND, serviceType));
                                }
                                var method = serviceType.GetRuntimeMethod(svc.Method, svc.Arguments.Select(o => o.GetType()).ToArray());
                                if (method == null)
                                {
                                    throw new EntryPointNotFoundException(String.Format(ErrorMessages.METHOD_NOT_FOUND, $"{svc.Method}({String.Join(",", svc.Arguments.Select(o => o.GetType()))})"));
                                }
                                method.Invoke(serviceInstance, svc.Arguments.ToArray());

                            }
                        }

                        context.Insert(new DbPatch() { ApplyDate = DateTimeOffset.Now, PatchId = patchId, Description = dataset.Id });
                        tx.Commit();

                        this.Installed?.Invoke(this, new DataPersistedEventArgs<Dataset>(dataset, TransactionMode.Commit, AuthenticationContext.Current.Principal));
                        return true;
                    }
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(String.Format(ErrorMessages.BUNDLE_PERSISTENCE_ERROR, "X", dataset.Id), e);
                }
            }
        }
    }
}
