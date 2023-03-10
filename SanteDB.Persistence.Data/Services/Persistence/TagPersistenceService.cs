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
 * Date: 2023-3-10
 */
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Acts;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence
{
    /// <summary>
    /// Tag persistence service
    /// </summary>
    public sealed class TagPersistenceService : ITagPersistenceService
    {
        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IDataCachingService m_dataCache;

        /// <summary>
        /// Creates a new tag persistence service
        /// </summary>
        public TagPersistenceService(IConfigurationManager configurationManager, IDataCachingService dataCachingService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_dataCache = dataCachingService;
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Data Tagging Service";

        /// <inheritdoc/>
        public void Save(Guid sourceKey, String tagName, String tagValue)
        {
            if (String.IsNullOrEmpty(tagName))
            {
                throw new ArgumentNullException(nameof(tagName), ErrorMessages.ARGUMENT_NULL);
            }
            else if (tagName.StartsWith("$")) // transient tag don't save
            {
                return;
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    var provenanceId = context.EstablishProvenance(AuthenticationContext.Current.Principal, null);
                    if (context.Any<DbEntity>(o => o.Key == sourceKey))
                    {
                        var existingTag = context.FirstOrDefault<DbEntityTag>(o => o.SourceKey == sourceKey && o.TagKey == tagName && o.ObsoletionTime == null);
                        if (existingTag != null)
                        {
                            // Update?
                            if (String.IsNullOrEmpty(tagValue))
                            {
                                existingTag.ObsoletedByKey = provenanceId;
                                existingTag.ObsoletionTime = DateTimeOffset.Now;
                            }
                            else
                            {
                                existingTag.Value = tagValue;
                            }
                            context.Update(existingTag);
                        }
                        else
                        {
                            context.Insert(new DbEntityTag()
                            {
                                CreatedByKey = provenanceId,
                                CreationTime = DateTimeOffset.Now,
                                SourceKey = sourceKey,
                                TagKey = tagName,
                                Value = tagValue
                            });
                        }
                    }
                    else if (context.Any<DbAct>(o => o.Key == sourceKey))
                    {
                        var existingTag = context.FirstOrDefault<DbActTag>(o => o.SourceKey == sourceKey && o.TagKey == tagName && o.ObsoletionTime == null);
                        if (existingTag != null)
                        {
                            // Update?
                            if (String.IsNullOrEmpty(tagValue))
                            {
                                existingTag.ObsoletedByKey = provenanceId;
                                existingTag.ObsoletionTime = DateTimeOffset.Now;
                            }
                            else
                            {
                                existingTag.Value = tagValue;
                            }
                            context.Update(existingTag);
                        }
                        else
                        {
                            context.Insert(new DbActTag()
                            {
                                CreatedByKey = provenanceId,
                                CreationTime = DateTimeOffset.Now,
                                SourceKey = sourceKey,
                                TagKey = tagName,
                                Value = tagValue
                            });
                        }
                    }
                    else
                    {
                        throw new NotSupportedException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(ITaggable), null));
                    }
                    this.m_dataCache.Remove(sourceKey);
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException($"Error adding tag to {sourceKey}", e);
                }
            }
        }

        /// <summary>
        /// Save the specified tag against the specified source key
        /// </summary>
        public void Save(Guid sourceKey, ITag tag)
        {
            this.Save(sourceKey, tag.TagKey, tag.Value);
        }
    }
}