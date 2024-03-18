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
using SanteDB.Core.Configuration;
using SanteDB.Docker.Core;
using SanteDB.Persistence.Data.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.Persistence.Data.Docker
{
    /// <summary>
    /// The ADO.NET freetext indexing docker feature
    /// </summary>
    public class AdoFreetextDockerFeature : IDockerFeature
    {
        /// <summary>
        /// Gets the identifier of this docker feature
        /// </summary>
        public string Id => "ADO_FTS";

        /// <summary>
        /// Gets the settings for this object
        /// </summary>
        public IEnumerable<string> Settings => new String[0];

        /// <summary>
        /// Configure the feature
        /// </summary>
        public void Configure(SanteDBConfiguration configuration, IDictionary<string, string> settings)
        {
            var serviceConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders;
            serviceConfiguration.Add(new TypeReferenceConfiguration(typeof(AdoFreetextSearchService)));
        }
    }
}
