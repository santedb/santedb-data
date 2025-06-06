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
using SanteDB.Core.Configuration;
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Represetns a job schedule that is stored in the database
    /// </summary>
    [Table("job_sch_systbl")]
    public class DbJobScheule : DbNonVersionedBaseData
    {
        /// <summary>
        /// Gets or sets the key of the job schedule
        /// </summary>
        [Column("job_sch_id"), PrimaryKey, AutoGenerated, NotNull]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the job identifier
        /// </summary>
        [Column("job_id"), NotNull]
        public Guid JobId { get; set; }

        /// <summary>
        /// Gets or sets the schedule type
        /// </summary>
        [Column("typ"), NotNull]
        public JobScheduleType Type { get; set; }

        /// <summary>
        /// Gets or sets the interval of the job
        /// </summary>
        [Column("ivl")]
        public String Interval { get; set; }

        /// <summary>
        /// Gets or sets the time when the schedule is effective
        /// </summary>
        [Column("start_utc"), NotNull]
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets the stop time
        /// </summary>
        [Column("stop_utc")]
        public DateTimeOffset? StopTime { get; set; }

        /// <summary>
        /// Gets or sets the days
        /// </summary>
        [Column("dow")]
        public byte[] Days { get; set; }

        /// <summary>
        /// Allows the ORM to set a null value over top of a column value
        /// </summary>
        public bool DaysSpecified { get; set; }
        /// <summary>
        /// Allows the ORM to set a null value over top of a column value
        /// </summary>
        public bool IntervalSpecified { get; set; }
        /// <summary>
        /// Allows the ORM to set a null value over top of a column value
        /// </summary>
        public bool StopTimeSpecified { get; set; }
    }
}
