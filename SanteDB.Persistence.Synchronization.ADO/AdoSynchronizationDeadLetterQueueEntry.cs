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
using SanteDB.Core.Model;
using SanteDB.Persistence.Synchronization.ADO.Model;

namespace SanteDB.Persistence.Synchronization.ADO
{

    /// <summary>
    /// Implementation of <see cref="ISynchronizationDeadLetterQueueEntry"/> for dead letter queue entries which are loaded from ADO
    /// </summary>
    internal class AdoSynchronizationDeadLetterQueueEntry : AdoSynchronizationQueueEntry, ISynchronizationDeadLetterQueueEntry
    {
        private readonly DbSynchronizationDeadLetterQueueEntry m_deadLetterSource;

        public AdoSynchronizationDeadLetterQueueEntry(AdoSynchronizationQueue currentQueue, ISynchronizationQueue originalQueue,  DbSynchronizationQueueEntry dbQueueEntry, DbSynchronizationDeadLetterQueueEntry dbDeadLetterEntry) : base(currentQueue, dbQueueEntry)
        {
            this.m_deadLetterSource = dbDeadLetterEntry;
            this.OriginalQueue = originalQueue;
        }

        /// <inheritdoc/>
        public ISynchronizationQueue OriginalQueue { get; }

        /// <inheritdoc/>
        public string ReasonForRejection => this.m_deadLetterSource.Reason;
    }
}