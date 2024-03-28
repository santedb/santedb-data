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
 * User: fyfej
 * Date: 2023-6-21
 */
using SanteDB.Core.Jobs;
using SanteDB.Persistence.Data.Model.Sys;
using System;

namespace SanteDB.Persistence.Data.Jobs
{
    /// <summary>
    /// Represetns an ADO job state
    /// </summary>
    internal class AdoJobState : IJobState
    {

        /// <summary>
        /// Creates a new job state
        /// </summary>
        public AdoJobState(DbJobState jobState, XmlJobState cacheInfo, IJob targetJob)
        {
            this.LastStartTime = jobState.LastStart?.DateTime;
            this.LastStopTime = jobState.LastStop?.DateTime;
            this.CurrentState = cacheInfo?.CurrentState ?? jobState.LastState;
            this.StatusText = cacheInfo?.StatusText;
            this.Progress = cacheInfo?.Progress ?? 0.0f;
            this.Job = targetJob;
        }

        /// <inheritdoc/>
        public IJob Job { get; }

        /// <inheritdoc/>
        public string StatusText { get; }

        /// <inheritdoc/>
        public float Progress { get; }

        /// <inheritdoc/>
        public JobStateType CurrentState { get; }

        /// <inheritdoc/>
        public DateTime? LastStartTime { get; }

        /// <inheritdoc/>
        public DateTime? LastStopTime { get; }
    }
}
