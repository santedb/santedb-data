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
using SanteDB.Core.Configuration;
using SanteDB.Core.Jobs;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Linq;
using System.Xml;

namespace SanteDB.Persistence.Data.Jobs
{
    /// <summary>
    /// Represents an implementation of <see cref="IJobSchedule"/> for ADO tables
    /// </summary>
    internal class AdoJobSchedule : IJobSchedule
    {

        /// <summary>
        /// Creates a new job shcedule based on the job schedul
        /// </summary>
        public AdoJobSchedule(DbJobScheule jobSchedule)
        {
            this.Key = jobSchedule.Key;
            this.Type = jobSchedule.Type;

            if (!String.IsNullOrEmpty(jobSchedule.Interval))
            {
                this.Interval = XmlConvert.ToTimeSpan(jobSchedule.Interval);
            }
            this.StartTime = jobSchedule.StartTime.DateTime;
            this.StopTime = jobSchedule.StopTime?.DateTime;

            if (jobSchedule.Days != null)
            {
                this.Days = jobSchedule.Days.Select(o => (DayOfWeek)o).ToArray();
            }
        }

        /// <summary>
        /// Gets the key of the job schedule for updating
        /// </summary>
        internal Guid Key { get; }

        /// <inheritdoc/>
        public JobScheduleType Type { get; }

        /// <inheritdoc/>
        public TimeSpan? Interval { get; }

        /// <inheritdoc/>
        public DateTime StartTime { get; }

        /// <inheritdoc/>
        public DateTime? StopTime { get; }

        /// <inheritdoc/>
        public DayOfWeek[] Days { get; }
    }
}
