using SanteDB.Core.Configuration;
using SanteDB.Core.Jobs;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
