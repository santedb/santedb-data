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
 * Date: 2024-6-21
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Model;
using SanteDB.Persistence.Synchronization.ADO.Model;
using System;

namespace SanteDB.Persistence.Synchronization.ADO.Queues
{
    /// <summary>
    /// An implementation of the <see cref="ISynchronizationQueueEntry"/> that is loaded from the ADO layer
    /// </summary>
    internal class AdoSynchronizationQueueEntry : ISynchronizationQueueEntry
    {
        private readonly DbSynchronizationQueueEntry m_queueEntry;
        private readonly AdoSynchronizationQueue m_sourceQueue;

        /// <summary>
        /// Create a synchronization queue entry
        /// </summary>
        public AdoSynchronizationQueueEntry(AdoSynchronizationQueue queue, DbSynchronizationQueueEntry dbQueueEntry)
        {
            m_queueEntry = dbQueueEntry;
            m_sourceQueue = queue;
        }

        /// <inheritdoc/>
        public int Id => m_queueEntry.Id;

        /// <inheritdoc/>
        public Guid CorrelationKey => m_queueEntry.CorrelationKey;

        /// <inheritdoc/>
        public DateTimeOffset CreationTime => m_queueEntry.CreationTime;

        /// <inheritdoc/>
        public string ResourceType => m_queueEntry.ResourceType;

        /// <inheritdoc/>
        public Guid DataFileKey => m_queueEntry.DataFileKey;

        /// <inheritdoc/>
        public IdentifiedData Data { get; internal set; }

        /// <inheritdoc/>
        public SynchronizationQueueEntryOperation Operation => m_queueEntry.Operation;

        /// <inheritdoc/>
        public int? RetryCount => m_queueEntry.RetryCount;

        /// <inheritdoc/>
        public ISynchronizationQueue Queue => m_sourceQueue;
    }
}