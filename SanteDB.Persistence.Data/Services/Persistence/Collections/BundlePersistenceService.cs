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
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
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
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using ZstdSharp.Unsafe;

namespace SanteDB.Persistence.Data.Services.Persistence.Collections
{
    /// <summary>
    /// Represents a persistence service that wraps and persists the objects in a bundle
    /// </summary>
    public sealed class BundlePersistenceService : IDataPersistenceService<Bundle>, IAdoPersistenceProvider<Bundle>, IDataPersistenceService, IReportProgressChanged
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BundlePersistenceService));

        // Localization service
        private readonly ILocalizationService m_localizationService;
        // Configuration service
        private readonly AdoPersistenceConfigurationSection m_configuration;
        // Model mapping utility
        private readonly ModelMapper m_modelMapper;
        // Data cache service
        private readonly IDataCachingService m_dataCachingService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public BundlePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IDataCachingService dataCachingService = null)
        {
            this.m_localizationService = localizationService;
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.Provider = this.m_configuration.Provider;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
            this.m_dataCachingService = dataCachingService;

        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Bundle Persistence Service";

        /// <inheritdoc/>
        public IDbProvider Provider { get; set; }

        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<Bundle>> Inserted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<Bundle>> Inserting;

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<Bundle>> Updated;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<Bundle>> Updating;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<Bundle>> Obsoleted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<Bundle>> Obsoleting;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<Bundle>> Deleted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<Bundle>> Deleting;
        /// <inheritdoc/>
        public event EventHandler<QueryResultEventArgs<Bundle>> Queried;
        /// <inheritdoc/>
        public event EventHandler<QueryRequestEventArgs<Bundle>> Querying;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievingEventArgs<Bundle>> Retrieving;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievedEventArgs<Bundle>> Retrieved;
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

#pragma warning restore CS0067

        /// <summary>
        /// Re-organize a bundle's contents for insert
        /// </summary>
        public Bundle ReorganizeForInsert(Bundle input)
        {
            var retVal = new Bundle(input.Item.Where(o => o.BatchOperation == BatchOperationType.Delete))
            {
                CorrelationKey = input.CorrelationKey,
                CorrelationSequence = input.CorrelationSequence
            };
            var resolved = input.Item.Where(e=>!retVal.Item.Contains(e)).ToArray();

            // Process each object in our queue of to be processed
            bool swapped = true;
            for (var outer = 0; outer < resolved.Length && swapped; outer++)
            {
                swapped = false;
                for (var inner = 0; inner < resolved.Length; inner++)
                {
                    var itm = resolved[inner];
                    IEnumerable<int> dependencies = new int[0];
                    switch (itm)
                    {
                        case Entity entity:
                            dependencies = dependencies.Concat(entity.Relationships?.Select(r => Array.FindIndex(resolved, k => k.Key == r.TargetEntityKey)) ?? new int[0]);
                            dependencies = dependencies.Concat(entity.Participations?.Select(p => Array.FindIndex(resolved, i => i.Key == p.ActKey)) ?? new int[0]);
                            if(entity.CreationActKey.HasValue && resolved.Any(r=>r.Key == entity.CreationActKey))
                            {
                                dependencies = dependencies.Concat(new int[] { Array.FindIndex(resolved, i => i.Key == entity.CreationActKey) });
                            }
                            break;
                        case Act act:
                            dependencies = dependencies.Concat(act.Relationships?.Select(r => Array.FindIndex(resolved, i => i.Key == r.TargetActKey)) ?? new int[0]);
                            dependencies = dependencies.Concat(act.Participations?.Select(p => Array.FindIndex(resolved, i => i.Key == p.PlayerEntityKey)) ?? new int[0]);
                            break;
                        case Concept concept:
                            dependencies = dependencies.Concat(concept.Relationships?.Select(r => Array.FindIndex(resolved, i => i.Key == r.TargetConceptKey)) ?? new int[0]);
                            break;
                        case ITargetedAssociation ta:
                            dependencies = new int[] { Array.FindIndex(resolved, i => i.Key == ta.TargetEntityKey), Array.FindIndex(resolved, i => i.Key == ta.SourceEntityKey) };
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

            retVal.AddRange(resolved);
            return retVal; 
        }


        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public long Count(Expression<Func<Bundle, bool>> query, IPrincipal authContext = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Delete(Guid key, TransactionMode transactionMode, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Get(Guid key, Guid? versionKey, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Bundle Insert(Bundle data, TransactionMode transactionMode, IPrincipal principal)
        {

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), ErrorMessages.ARGUMENT_NULL);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }
            else if(data.Item.All(o=>o.BatchOperation == BatchOperationType.Ignore) || !data.Item.Any())
            {
                this.m_tracer.TraceWarning("Ignoring bundle - all items are set to IGNORE");
                return data;
            }

            var preEventArgs = new DataPersistingEventArgs<Bundle>(data, transactionMode, principal);
            this.Inserting?.Invoke(this, preEventArgs);
            if (preEventArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Persistence event on Bundle signals cancel");
                return preEventArgs.Data;
            }

            var deferConstraints = data.ShouldDisablePersistenceConstraints();
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    using (var tx = context.BeginTransaction())
                    {
                        try
                        {
                            if (deferConstraints)
                            {
                                context.DisableConstraints();
                            }
                            context.EstablishProvenance(principal, null);

                            // Correlation and message control
                            // JF - 20250127 - This set of code will register the bundle's correlation key and sequence into the database and will perform necessary actions
                            // to prevent changes where none is desired. 
                            if (this.ValidateBundleCorrelation(context, data))
                            {

                                data = data.HarmonizeKeys(KeyHarmonizationMode.KeyOverridesProperty, this.m_configuration.StrictKeyAgreement);

                                data = this.Insert(context, data);

                                data = data.HarmonizeKeys(KeyHarmonizationMode.PropertyOverridesKey, this.m_configuration.StrictKeyAgreement);

                                if (transactionMode == TransactionMode.Commit)
                                {
                                    tx.Commit();
                                }

                                data.Item.ForEach(i => this.m_dataCachingService.Add(i));
                            }
                        }
                        finally
                        {
                            if (deferConstraints)
                            {
                                context.RestoreConstraints();
                            }
                        }
                    }
                }

                // Post event
                var postEventData = new DataPersistedEventArgs<Bundle>(data, transactionMode, principal);
                this.Inserted?.Invoke(this, postEventData);
                return postEventData.Data;
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
            }
        }

        /// <summary>
        /// Validate the bundle sequence and insert the correlation key if needed 
        /// </summary>
        private bool ValidateBundleCorrelation(DataContext context, Bundle data)
        {
            if(context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            else if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // The bundle is un-correlated so don't check
            if(!data.CorrelationKey.HasValue)
            {
                return true;
            }

            // We will determine if we've processed this bundle before
            var lastSequence = context.Query<DbBundleCorrelationSubmission>(o => o.SourceKey == data.CorrelationKey.Value).OrderByDescending(o => o.CorrelationSequence).Select(o => o.CorrelationSequence).FirstOrDefault();
            if (!lastSequence.HasValue) // Never submitted before
            {
                context.Insert(new DbBundleCorrelation() { Key = data.CorrelationKey.Value });
            }
            else if (lastSequence > data.CorrelationSequence) // We already received a future sequence so ignore this one
            {
                switch (this.m_configuration.BundleCorrelationBehavior) {
                    case BundleCorrelationBehaviorType.NotModified:
                        throw new PreconditionFailedException(String.Format(ErrorMessages.BUNDLE_CORRELATION_SEQUENCE_ERROR, data.CorrelationSequence, lastSequence));
                    case BundleCorrelationBehaviorType.Ignore:
                        return false;
                    case BundleCorrelationBehaviorType.ThrowError:
                        throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "error.persistence.sequence", String.Format(ErrorMessages.BUNDLE_CORRELATION_SEQUENCE_ERROR, data.CorrelationSequence, lastSequence), DetectedIssueKeys.InvalidDataIssue, null);
                }
            }

            // Assign a bundle id to uniquely track the message
            data.Key = data.Key ?? Guid.NewGuid();
            
            // Already processed? 
            if(context.Any<DbBundleCorrelationSubmission>(o=>o.Key == data.Key))
            {
                switch(this.m_configuration.BundleCorrelationBehavior)
                {
                    case BundleCorrelationBehaviorType.NotModified:
                        throw new PreconditionFailedException(String.Format(ErrorMessages.BUNDLE_ALREADY_PROCESSED, data.Key));
                    case BundleCorrelationBehaviorType.Ignore:
                        return false;
                    case BundleCorrelationBehaviorType.ThrowError:
                        throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "error.persistence.alreadyDone", String.Format(ErrorMessages.BUNDLE_ALREADY_PROCESSED, data.Key), DetectedIssueKeys.AlreadyDoneIssue, null);
                }
            }

            context.Insert(new DbBundleCorrelationSubmission()
            {
                Key = data.Key.Value,
                SourceKey = data.CorrelationKey.Value,
                CreatedByKey = context.ContextId,
                CreationTime = DateTimeOffset.Now,
                CorrelationSequence = data.CorrelationSequence
            });
            return true;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public IQueryResultSet<Bundle> Query(Expression<Func<Bundle, bool>> query, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public IEnumerable<Bundle> Query(Expression<Func<Bundle, bool>> query, int offset, int? count, out int totalResults, IPrincipal principal, params ModelSort<Bundle>[] orderBy)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <see cref="Insert(Bundle, TransactionMode, IPrincipal)"/>
        public Bundle Update(Bundle data, TransactionMode transactionMode, IPrincipal principal)
        {
            return this.Insert(data, transactionMode, principal);
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public IQueryResultSet<Bundle> Query(OrmLite.DataContext context, Expression<Func<Bundle, bool>> filter)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Bundle Insert(OrmLite.DataContext context, Bundle data)
        {
            
            // Process the bundle components
            data = this.ReorganizeForInsert(data);
            for (var i = 0; i < data.Item.Count; i++)
            {
                if (i % 100 == 0)
                {
                    this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(BundlePersistenceService), (float)i / (float)data.Item.Count, UserMessages.PROCESSING));
                }

                var objectType = data.Item[i].GetType();
                if (data.Item[i] is IdentifiedDataReference idr)
                {
                    if(idr.BatchOperation != BatchOperationType.Delete)
                    {
                        throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, "Insert on IdentifiedDataReference"));
                    }
                    objectType = idr.ReferencedType;
                }

                var persistenceService = objectType.GetRelatedPersistenceService();
                // Is the object a reference?

                try
                {

                    switch (data.Item[i].BatchOperation)
                    {
                        case BatchOperationType.Delete:
                            if (persistenceService.Exists(context, data.Item[i].Key.Value))
                            {
                                data.Item[i] = persistenceService.Delete(context, data.Item[i].Key.Value, DataPersistenceControlContext.Current?.DeleteMode ?? this.m_configuration.DeleteStrategy);
                                data.Item[i].BatchOperation = BatchOperationType.Delete;
                            }
                            else
                            {
                                data.Item[i].BatchOperation = BatchOperationType.Ignore;
                            }
                            break;
                        case BatchOperationType.Insert:
                            data.Item[i] = persistenceService.Insert(context, data.Item[i]);
                            data.Item[i].BatchOperation = BatchOperationType.Insert;
                            break;
                        case BatchOperationType.Update:
                            data.Item[i] = persistenceService.Update(context, data.Item[i]);
                            data.Item[i].BatchOperation = BatchOperationType.Update;
                            break;
                        case BatchOperationType.InsertOrUpdate:
                        case BatchOperationType.Auto:
                            
                            // Ensure that the object exists
                            if (data.Item[i].Key.HasValue && persistenceService.Exists(context, data.Item[i].Key.Value))
                            {
                                data.Item[i] = persistenceService.Update(context, data.Item[i]);
                                data.Item[i].BatchOperation = BatchOperationType.Update;
                            }
                            else
                            {
                                data.Item[i] = persistenceService.Insert(context, data.Item[i]);
                                data.Item[i].BatchOperation = BatchOperationType.Insert;
                            }
                            break;
                    }

                    // Add to the context dictionary so others know that this was added
                    if (!context.Data.ContainsKey(data.Item[i].Key.ToString()))
                    {
                        context.Data.Add(data.Item[i].Key.ToString(), data.Item[i].BatchOperation);
                    }
                }
                catch (DbException e)
                {
                    throw new DataPersistenceException(String.Format(ErrorMessages.BUNDLE_PERSISTENCE_ERROR, i, data.Item[i]), e.TranslateDbException());
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(String.Format(ErrorMessages.BUNDLE_PERSISTENCE_ERROR, i, data.Item[i]), e);
                }
            }

            // Give the bundle a UUID indicating it was persisted (use the prov id)
            data.Key = context.ContextId;
            return data;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Update(OrmLite.DataContext context, Bundle data)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Delete(OrmLite.DataContext context, Guid key, DeleteMode deletionMode)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Get(OrmLite.DataContext context, Guid key)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        object IAdoPersistenceProvider.Touch(DataContext context, Guid id) => this.Touch(context, id);

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public Bundle Touch(OrmLite.DataContext context, Guid id)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IdentifiedData Insert(OrmLite.DataContext context, IdentifiedData data)
        {
            return this.Insert(context, (Bundle)data);
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        public IdentifiedData Update(OrmLite.DataContext context, IdentifiedData data)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on <see cref="Bundle"/></exception>
        IdentifiedData IAdoPersistenceProvider.Delete(OrmLite.DataContext context, Guid key, DeleteMode deleteMode)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        object IDataPersistenceService.Insert(object data) => this.Insert((Bundle)data, TransactionMode.Commit, AuthenticationContext.Current.Principal);

        /// <inheritdoc/>
        object IDataPersistenceService.Update(object data) => this.Update((Bundle)data, TransactionMode.Commit, AuthenticationContext.Current.Principal);

        /// <inheritdoc />
        object IDataPersistenceService.Delete(Guid id) => this.Delete(id, TransactionMode.Commit, AuthenticationContext.Current.Principal);

        /// <inheritdoc />
        object IDataPersistenceService.Get(Guid id) => this.Get(id, null, AuthenticationContext.Current.Principal);

        /// <inheritdoc />
        IEnumerable IDataPersistenceService.Query(Expression query, int offset, int? count, out int totalResults) => this.Query(query as Expression<Func<Bundle, bool>>, offset, count, out totalResults, AuthenticationContext.Current.Principal);

        /// <inheritdoc />
        IQueryResultSet IDataPersistenceService.Query(Expression query)
        {
            if (query is Expression<Func<Bundle, bool>> expr)
            {
                return this.Query(expr, AuthenticationContext.Current.Principal);
            }
            else
            {
                throw new ArgumentException(nameof(query), String.Format(ErrorMessages.ARGUMENT_INVALID_TYPE, typeof(Expression<Func<Bundle, bool>>), query.GetType()));
            }
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported on bundles</exception>
        public bool Exists(OrmLite.DataContext context, Guid key)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<Bundle> Query<TExpression>(Expression<Func<TExpression, bool>> query, IPrincipal principal) where TExpression : Bundle
        {
            throw new NotSupportedException();
        }
    }
}
