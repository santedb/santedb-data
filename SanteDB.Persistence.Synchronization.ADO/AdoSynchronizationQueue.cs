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
 * Date: 2023-5-19
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data;
using SanteDB.Persistence.Synchronization.ADO.Model;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using static SanteDB.Core.Services.Impl.FileSystemDispatcherQueueService;

namespace SanteDB.Persistence.Synchronization.ADO
{
    /// <summary>
    /// An implementation of the <see cref="ISynchronizationQueue"/> which uses a database for storage of queue item metadata whilst
    /// storing the actual blobs of data via the <see cref="IDataStreamManager"/>
    /// </summary>
    internal class AdoSynchronizationQueue : ISynchronizationQueue
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoSynchronizationQueue));
        private readonly ISynchronizationQueueManager m_synchronizationManager;
        private readonly ModelMapper m_mapper;
        private readonly IDataStreamManager m_dataStreamManager;
        private readonly IDbProvider m_dataProvider;
        private readonly DbSynchronizationQueue m_queueRecord;
        
        // Format of the messages in the data stream
        private readonly IBodySerializer m_messageSerializer = new XmlBodySerializer();  // new JsonBodySerializer();
        private readonly ModelSerializationBinder m_modelSerializationBinder = new ModelSerializationBinder();

        // Synchronization lock
        private readonly object m_lock = new object();

        /// <summary>
        /// Creates a new ADO synchronization instance
        /// </summary>
        /// <param name="dataStreamManager">The data stream manager which should be used for storing the data</param>
        /// <param name="dataProvider">The data provider to the database</param>
        /// <param name="queueRecord">The database queue registration for this queue</param>
        /// <param name="mapper">The instance of the shared model mapper</param>
        /// <param name="adoSynchronizationManager">The instance of the <see cref="ISynchronizationQueueManager"/> which holds this queue</param>
        internal AdoSynchronizationQueue(ISynchronizationQueueManager adoSynchronizationManager, IDataStreamManager dataStreamManager, IDbProvider dataProvider, ModelMapper mapper, DbSynchronizationQueue queueRecord)
        {
            this.m_synchronizationManager = adoSynchronizationManager;
            this.m_mapper = mapper;
            this.m_dataStreamManager = dataStreamManager;
            this.m_dataProvider = dataProvider;
            this.m_queueRecord = queueRecord;
        }

        /// <summary>
        /// Gets the UUID of this queue
        /// </summary>
        internal Guid Uuid => this.m_queueRecord.Key;

        /// <inheritdoc/>
        public string Name => this.m_queueRecord.Name;

        /// <inheritdoc/>
        public SynchronizationPattern Type => this.m_queueRecord.Type;

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_dataProvider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => null;

        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<ISynchronizationQueueEntry>> Enqueuing;

        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<ISynchronizationQueueEntry>> Enqueued;

        /// <summary>
        /// Get the next queue entry
        /// </summary>
        private ISynchronizationQueueEntry GetNextQueueEntry(DataContext context)
        {
            var query = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSynchronizationQueueEntry), typeof(DbSynchronizationDeadLetterQueueEntry), typeof(DbSynchronizationQueue))
                      .Join<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry>("LEFT", o => o.Id, o => o.Id)
                      .Join<DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>("LEFT", o=>o.OriginalQueue, o=>o.Key)
                      .Where<DbSynchronizationQueueEntry>(o => o.QueueKey == this.m_queueRecord.Key)
                      .OrderBy<DbSynchronizationQueueEntry>(o => o.Id, Core.Model.Map.SortOrderType.OrderBy);

            var dbData = context.FirstOrDefault<CompositeResult<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>>(query.Statement);

            if (dbData == null)
            {
                return null;
            }
            else
            {
                var retVal = dbData.Object2.OriginalQueue != Guid.Empty ?
                                new AdoSynchronizationDeadLetterQueueEntry(this, this.m_synchronizationManager.Get(dbData.Object3.Name), dbData.Object1, dbData.Object2) :
                                new AdoSynchronizationQueueEntry(this, dbData.Object1);


                using (var queueData = this.m_dataStreamManager.Get(dbData.Object1.DataFileKey))
                {
                    retVal.Data = (IdentifiedData)this.m_messageSerializer.DeSerialize(queueData, new System.Net.Mime.ContentType(dbData.Object1.ContentType), this.m_modelSerializationBinder.BindToType(null, dbData.Object1.ResourceType));
                }

                return retVal;
            }
        }

        /// <inheritdoc/>
        public int Count()
        {
            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetReadonlyConnection())
                    {
                        context.Open();
                        return (int)context.Count<DbSynchronizationQueueEntry>(o => o.QueueKey == this.m_queueRecord.Key);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(String.Format(ErrorMessages.READ_ERROR, $"{nameof(AdoSynchronizationQueue)}/{this.Name}"), e);
            }
        }

        /// <inheritdoc/>
        public void Delete(int id)
        {
            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetWriteConnection())
                    {
                        context.Open();

                        var queueEntry = context.FirstOrDefault<DbSynchronizationQueueEntry>(o => o.Id == id && o.QueueKey == this.m_queueRecord.Key);

                        if (queueEntry == null)
                        {
                            throw new KeyNotFoundException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, $"{nameof(AdoSynchronizationQueue)}/{this.Name}/{id}"));
                        }

                        using (var tx = context.BeginTransaction())
                        {

                            // Delete the record 
                            context.Delete(queueEntry);

                            // Delete data stream
                            if (!context.Any<DbSynchronizationQueueEntry>(o => o.DataFileKey == queueEntry.DataFileKey)) // no other active entry is referencing the data file?
                            {
                                this.m_tracer.TraceVerbose("Delete queue datastream {0}", queueEntry.DataFileKey);
                                this.m_dataStreamManager.Remove(queueEntry.DataFileKey);
                            }
                            tx.Commit();
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
                throw new DataPersistenceException(String.Format(ErrorMessages.WRITE_ERROR, $"{nameof(AdoSynchronizationQueue)}/{this.Name}/{id}"), e);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry Dequeue()
        {
            try
            {
                lock(this.m_lock) {
                    using (var context = this.m_dataProvider.GetWriteConnection())
                    {
                        context.Open();

                        var nextEntry = this.GetNextQueueEntry(context);

                        // We want to read the queue entry data and delete since the requestor is calling dequeue
                        using (var tx = context.BeginTransaction())
                        {

                            context.DeleteAll<DbSynchronizationDeadLetterQueueEntry>(o => o.Id == nextEntry.Id);
                            context.DeleteAll<DbSynchronizationQueueEntry>(o => o.Id == nextEntry.Id);

                            // Delete data stream
                            if (!context.Any<DbSynchronizationQueueEntry>(o => o.DataFileKey == nextEntry.DataFileKey)) // no other active entry is referencing the data file?
                            {
                                this.m_tracer.TraceVerbose("Delete queue datastream {0}", nextEntry.DataFileKey);
                                this.m_dataStreamManager.Remove(nextEntry.DataFileKey);
                            }

                            tx.Commit();

                            return nextEntry;
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
                throw new DataPersistenceException(String.Format(ErrorMessages.WRITE_ERROR, $"DQ {nameof(AdoSynchronizationQueue)}/{this.Name}"), e);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry Enqueue(IdentifiedData data, SynchronizationQueueEntryOperation operation)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            else if (this.Type.HasFlag(SynchronizationPattern.DeadLetter))
            {
                throw new NotSupportedException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(Enqueue)));
            }

            var preEvent = new DataPersistingEventArgs<ISynchronizationQueueEntry>(new AdoSynchronizationQueueEntry(this, new DbSynchronizationQueueEntry() {  Operation = operation }) { Data = data }, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            this.Enqueuing?.Invoke(this, preEvent);
            if(preEvent.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event signals cancel of enqueue operation");
                return preEvent.Data;
            }

            try
            {
                lock(this.m_lock) {
                    using (var context = this.m_dataProvider.GetWriteConnection())
                    {
                        context.Open();
                        using (var tx = context.BeginTransaction())
                        {

                            this.m_modelSerializationBinder.BindToName(data.GetType(), out _, out var typeName);

                            var queueEntry = new DbSynchronizationQueueEntry()
                            {
                                Operation = operation,
                                ResourceType = typeName,
                                CorrelationKey = data.Key ?? Guid.NewGuid(),
                                QueueKey = this.m_queueRecord.Key
                            };

                            // First, store the object in the identified data - compressing the data 
                            try
                            {


                                using (var ms = new MemoryStream())
                                {
                                    this.m_messageSerializer.Serialize(ms, data, out var contentType);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    queueEntry.DataFileKey = this.m_dataStreamManager.Add(ms);
                                    queueEntry.ContentType = contentType.ToString();
                                }

                                queueEntry = context.Insert(queueEntry);

                                tx.Commit();

                                var retVal = new AdoSynchronizationQueueEntry(this, queueEntry) { Data = data };

                                this.Enqueued?.Invoke(this, new DataPersistedEventArgs<ISynchronizationQueueEntry>(retVal, TransactionMode.Commit, AuthenticationContext.Current.Principal));
                                return retVal;
                            }
                            catch
                            {
                                if (queueEntry.DataFileKey != Guid.Empty)
                                {
                                    this.m_dataStreamManager.Remove(queueEntry.DataFileKey); // Undo data stream storage
                                }
                                throw;
                            }
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
                throw new DataPersistenceException(String.Format(ErrorMessages.WRITE_ERROR, $"EQ {nameof(AdoSynchronizationQueue)}/{this.Name}"), e);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry Enqueue(ISynchronizationQueueEntry otherQueueEntry, String reasonText = null)
        {
            // Enqueue another entry from another queue into this queue - moving it
            if (otherQueueEntry == null)
            {
                throw new ArgumentNullException(nameof(otherQueueEntry));
            }
            else if(this.m_queueRecord.Type.HasFlag(SynchronizationPattern.DeadLetter) && String.IsNullOrEmpty(reasonText))
            {
                throw new ArgumentNullException(nameof(reasonText));
            }
            if(!(otherQueueEntry.Queue is AdoSynchronizationQueue otherAdoQueue))
            {
                throw new ArgumentException(nameof(otherQueueEntry), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, otherQueueEntry.Queue.GetType(), typeof(AdoSynchronizationQueue)));
            }

            if (otherQueueEntry is ISynchronizationDeadLetterQueueEntry &&
                this.Type.HasFlag(SynchronizationPattern.DeadLetter))
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, "DeadLetterQueueEntry cannot be copied to DeadLetter"));
            }
            

            var preEvent = new DataPersistingEventArgs<ISynchronizationQueueEntry>(otherQueueEntry, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            this.Enqueuing?.Invoke(this, preEvent);
            if (preEvent.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-event signals cancel of enqueue operation");
                return preEvent.Data;
            }

            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetWriteConnection())
                    {
                        context.Open();

                        var otherQueueObject = context.FirstOrDefault<DbSynchronizationQueueEntry>(o => o.Id == otherQueueEntry.Id);
                        if(otherQueueObject == null)
                        {
                            throw new KeyNotFoundException($"{nameof(AdoSynchronizationQueue)}/{otherQueueEntry.Queue.Name}/{otherQueueEntry.Id}");
                        }
                        using (var tx = context.BeginTransaction())
                        {

                            // Create a new queue entry
                            var newQueueEntry = new DbSynchronizationQueueEntry()
                            {
                                CorrelationKey = otherQueueObject.CorrelationKey,
                                CreationTime = DateTimeOffset.Now,
                                Operation = otherQueueObject.Operation,
                                DataFileKey = otherQueueObject.DataFileKey,
                                ResourceType = otherQueueObject.ResourceType,
                                ContentType = otherQueueObject.ContentType,
                                QueueKey = this.m_queueRecord.Key
                            };

                            if (otherQueueEntry is ISynchronizationDeadLetterQueueEntry idlqe)
                            {
                                newQueueEntry.RetryCount = (otherQueueEntry.RetryCount ?? 0) + 1;
                            }

                            newQueueEntry = context.Insert(newQueueEntry);

                            ISynchronizationQueueEntry retVal = null;
                            if (this.Type.HasFlag(SynchronizationPattern.DeadLetter))
                            {
                                var dlQueueEntry = context.Insert(new DbSynchronizationDeadLetterQueueEntry()
                                {
                                    Id = newQueueEntry.Id,
                                    OriginalQueue = otherAdoQueue.Uuid,
                                    Reason = reasonText
                                });
                                retVal = new AdoSynchronizationDeadLetterQueueEntry(this, otherAdoQueue, newQueueEntry, dlQueueEntry)
                                {
                                    Data = otherQueueEntry.Data
                                };
                            }
                            else
                            {
                                retVal = new AdoSynchronizationQueueEntry(this, newQueueEntry)
                                {
                                    Data = otherQueueEntry.Data
                                };
                            }

                            tx.Commit();

                            this.Enqueued?.Invoke(this, new DataPersistedEventArgs<ISynchronizationQueueEntry>(retVal, TransactionMode.Commit, AuthenticationContext.Current.Principal));
                            return retVal;
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
                throw new DataPersistenceException(String.Format(ErrorMessages.WRITE_ERROR, $"EQ {nameof(AdoSynchronizationQueue)}/{this.Name}/{otherQueueEntry.CorrelationKey}"), e);
            }
        }


        /// <inheritdoc/>
        public ISynchronizationQueueEntry Get(int id)
        {
            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetReadonlyConnection())
                    {
                        context.Open();


                        var query = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSynchronizationQueueEntry), typeof(DbSynchronizationDeadLetterQueueEntry), typeof(DbSynchronizationQueue))
                            .Join<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry>("LEFT", o => o.Id, o => o.Id)
                            .Join<DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>("LEFT", o => o.OriginalQueue, o => o.Key)
                            .Where<DbSynchronizationQueueEntry>(o => o.QueueKey == this.m_queueRecord.Key && o.Id == id)
                            .OrderBy<DbSynchronizationQueueEntry>(o => o.Id, Core.Model.Map.SortOrderType.OrderBy);

                        var queueEntry = context.FirstOrDefault<CompositeResult<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>>(query.Statement);

                        if (queueEntry == null)
                        {
                            throw new KeyNotFoundException($"{nameof(AdoSynchronizationQueue)}/{this.Name}/{id}");
                        }

                        var retVal = queueEntry.Object2.OriginalQueue != Guid.Empty ?
                               new AdoSynchronizationDeadLetterQueueEntry(this, this.m_synchronizationManager.Get(queueEntry.Object3.Name), queueEntry.Object1, queueEntry.Object2) :
                               new AdoSynchronizationQueueEntry(this, queueEntry.Object1);


                        using (var queueData = this.m_dataStreamManager.Get(queueEntry.Object1.DataFileKey))
                        {
                            retVal.Data = (IdentifiedData)this.m_messageSerializer.DeSerialize(queueData, new System.Net.Mime.ContentType(queueEntry.Object1.ContentType), this.m_modelSerializationBinder.BindToType(null, queueEntry.Object1.ResourceType));
                        }

                        return retVal;
                    }
                }
            }
            catch(DbException e)
            {
                throw e.TranslateDbException();
            }
            catch(Exception e)
            {
                throw new DataPersistenceException(String.Format(ErrorMessages.READ_ERROR, $"{nameof(AdoSynchronizationQueue)}/{this.Name}/{id}"), e);
            }
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry Peek()
        {
            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetReadonlyConnection())
                    {
                        context.Open();

                        var entry = this.GetNextQueueEntry(context);
                        return entry;
                    }
                }
            }
            catch(DbException e)
            {
                throw e.TranslateDbException();
            }
            catch(Exception e)
            {
                throw new DataPersistenceException(String.Format(ErrorMessages.READ_ERROR, $"PK {nameof(AdoSynchronizationQueue)}/{this.Name}"), e);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Query(Expression<Func<ISynchronizationQueueEntry, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            try
            {
                lock (this.m_lock)
                {
                    using (var context = this.m_dataProvider.GetReadonlyConnection())
                    {
                        context.Open();
                        SqlStatement domainQuery = null;
                        var domainLambda = this.m_mapper.MapModelExpression<ISynchronizationQueueEntry, DbSynchronizationQueueEntry, bool>(query, false);
                        if (domainLambda == null)
                        {
                            domainQuery = context.GetQueryBuilder(this.m_mapper).CreateQuery<ISynchronizationQueueEntry>(query).Statement;
                        }
                        else
                        {
                            domainQuery = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSynchronizationQueueEntry)).Where(domainLambda).Statement;
                        }

                        // Now we want to query 
                        var matchingKeys = context.Query<DbSynchronizationQueueEntry>(domainQuery).Where(o=>o.QueueKey == this.m_queueRecord.Key).Select(o => o.Id).ToArray();

                        domainQuery = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSynchronizationQueueEntry), typeof(DbSynchronizationDeadLetterQueueEntry), typeof(DbSynchronizationQueue))
                            .Join<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry>("LEFT", o => o.Id, o => o.Id)
                            .Join<DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>("LEFT", o => o.OriginalQueue, o => o.Key)
                            .Where<DbSynchronizationQueueEntry>(o => matchingKeys.Contains(o.Id))
                            .Statement;

                        return new MemoryQueryResultSet<ISynchronizationQueueEntry>(context.Query<CompositeResult<DbSynchronizationQueueEntry, DbSynchronizationDeadLetterQueueEntry, DbSynchronizationQueue>>(domainQuery)
                            .ToArray()
                            .Select(o => o.Object2.OriginalQueue == Guid.Empty ? new AdoSynchronizationQueueEntry(this, o.Object1) : new AdoSynchronizationDeadLetterQueueEntry(this, this.m_synchronizationManager.Get(o.Object3.Name), o.Object1, o.Object2))
                        );
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(String.Format(ErrorMessages.READ_ERROR, $"{nameof(AdoSynchronizationQueue)}/{this.Name}?{query}"), e);
            }
        }
    }
}