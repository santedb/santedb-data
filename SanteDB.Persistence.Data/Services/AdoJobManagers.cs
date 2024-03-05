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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Jobs;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Xml;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// A job manager which manages schedules and states 
    /// </summary>
    public class AdoJobManager : IJobScheduleManager, IJobStateManagerService
    {

        // Tracer.
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoJobManager));
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ILocalizationService m_localizationService;
        private readonly IAdhocCacheService m_adhocCache;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoJobManager(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_localizationService = localizationService;
            this.m_adhocCache = adhocCacheService;
        }

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, IJobSchedule jobSchedule)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }
            else if (jobSchedule == null)
            {
                throw new ArgumentNullException(nameof(jobSchedule));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction);

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {

                        var provId = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        var existing = context.FirstOrDefault<DbJobScheule>(o => o.JobId == job.Id && o.ObsoletionTime == null);
                        if (existing == null)
                        {
                            existing = context.Insert(new DbJobScheule()
                            {
                                CreatedByKey = provId,
                                Days = jobSchedule.Days?.Select(o => (byte)o).ToArray(),
                                Interval = jobSchedule.Interval.HasValue ? XmlConvert.ToString(jobSchedule.Interval.Value) : null,
                                JobId = job.Id,
                                StartTime = jobSchedule.StartTime,
                                StopTime = jobSchedule.StopTime,
                                Type = jobSchedule.Type
                            });
                        }
                        else
                        {
                            existing.UpdatedByKey = provId;
                            existing.UpdatedTime = DateTimeOffset.Now;
                            existing.Days = jobSchedule.Days?.Select(o => (byte)o).ToArray();
                            existing.DaysSpecified = true;
                            existing.Interval = jobSchedule.Interval.HasValue ? XmlConvert.ToString(jobSchedule.Interval.Value) : null;
                            existing.IntervalSpecified = true;
                            existing.StartTime = jobSchedule.StartTime;
                            existing.StopTime = jobSchedule.StopTime;
                            existing.StopTimeSpecified = true;
                            existing = context.Update(existing);
                        }

                        tx.Commit();

                        return new AdoJobSchedule(existing);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error assigning schedule: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_SCHEDULE_ASSIGN), e);
            }

        }

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, TimeSpan interval, DateTime? stopDate = null) => this.Add(job, new JobItemSchedule(interval, stopDate));

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, DayOfWeek[] repeatOn, DateTime startDate, DateTime? stopDate = null) => this.Add(job, new JobItemSchedule(repeatOn, startDate, stopDate));

        /// <inheritdoc/>
        public void Clear(IJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction);

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    var provId = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                    context.UpdateAll<DbJobScheule>(o => o.JobId == job.Id, o => o.ObsoletionTime == DateTimeOffset.Now, o => o.ObsoletedByKey == provId);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error clearing schedule: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_SCHEDULE_ASSIGN), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IJobSchedule> Get(IJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }


            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open();
                    return context.Query<DbJobScheule>(o => o.JobId == job.Id && o.ObsoletionTime == null).ToList().Select(o => new AdoJobSchedule(o));
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting schedule: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_SCHEDULE_QUERY), e);
            }
        }

        /// <inheritdoc/>
        public IJobState GetJobState(IJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    // The cache is the source (i.e. we don't persist state and stuff in DB) but it is supplemented by data in the db
                    context.Open();
                    var cacheStatus = this.m_adhocCache.Get<XmlJobState>($"sts.job.{job.Id}");
                    var dbStatus = context.FirstOrDefault<DbJobState>(o => o.JobId == job.Id);
                    return new AdoJobState(dbStatus, cacheStatus, job);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting job state: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_STATE_QUERY), e);
            }
        }

        /// <inheritdoc/>
        public void SetProgress(IJob job, string statusText, float progress)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    // The cache is the source (i.e. we don't persist state and stuff in DB) but it is supplemented by data in the db
                    context.Open();
                    var cacheStatus = this.m_adhocCache.Get<XmlJobState>($"sts.job.{job.Id}");
                    var dbStatus = context.FirstOrDefault<DbJobState>(o => o.JobId == job.Id);
                    if (cacheStatus == null)
                    {
                        cacheStatus = new XmlJobState()
                        {
                            JobId = job.Id,
                            CurrentState = JobStateType.Running,
                            LastStartTime = dbStatus?.LastStart?.DateTime ?? DateTime.Now,
                            LastStopTime = dbStatus?.LastStop?.DateTime,
                            Progress = progress,
                            StatusText = statusText
                        };
                    }
                    else
                    {
                        cacheStatus.StatusText = statusText;
                        cacheStatus.Progress = progress;
                    }
                    this.m_adhocCache.Add($"sts.job.{job.Id}", cacheStatus);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting job state: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_STATE_SET), e);
            }
        }

        /// <inheritdoc/>
        public void SetState(IJob job, JobStateType state, String statusText)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    // The cache is the source (i.e. we don't persist state and stuff in DB) but it is supplemented by data in the db
                    context.Open();
                    var cacheStatus = this.m_adhocCache.Get<XmlJobState>($"sts.job.{job.Id}");
                    var dbStatus = context.FirstOrDefault<DbJobState>(o => o.JobId == job.Id);

                    if (cacheStatus == null)
                    {
                        cacheStatus = new XmlJobState()
                        {
                            JobId = job.Id,
                            CurrentState = state,
                            Progress = 0.0f,
                            StatusText = statusText,
                            LastStartTime = state == JobStateType.Starting || state == JobStateType.Running ? DateTime.Now : dbStatus?.LastStart?.DateTime,
                        };
                    }
                    if (dbStatus == null)
                    {
                        dbStatus = context.Insert(new DbJobState()
                        {
                            JobId = job.Id,
                            LastStart = DateTimeOffset.Now,
                            LastState = JobStateType.Running,
                        });
                    }

                    switch (state)
                    {
                        case JobStateType.Running:
                            if (!cacheStatus.IsRunning())
                            {
                                dbStatus.LastStart = cacheStatus.LastStartTime = DateTime.Now;
                                dbStatus.LastStop = cacheStatus.LastStopTime = null;
                                dbStatus.LastStopSpecified = true;
                            }
                            break;
                        case JobStateType.Starting:
                            dbStatus.LastStart = cacheStatus.LastStartTime = DateTime.Now;
                            dbStatus.LastStop = cacheStatus.LastStopTime = null;
                            break;
                        case JobStateType.Completed:
                            dbStatus.LastStop = cacheStatus.LastStopTime = DateTime.Now;
                            break;
                    }
                    dbStatus.LastState = cacheStatus.CurrentState = state;
                    dbStatus.LastStatus = statusText;
                    dbStatus.LastStatusSpecified = true;

                    context.Update(dbStatus);
                    this.m_adhocCache.Add($"sts.job.{job.Id}", cacheStatus);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting job state: {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.JOB_STATE_SET), e);
            }
        }
    }
}
