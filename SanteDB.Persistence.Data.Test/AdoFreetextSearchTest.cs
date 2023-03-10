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
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Jobs;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Persistence.Data.Test
{
    /// <summary>
    /// Tests for the ADO.NET freetext search service
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class AdoFreetextSearchTest : DataPersistenceTest
    {

        /// <summary>
        /// Tests that the basic freetext search service works properly
        /// </summary>
        [Test]
        public void TestBasicFreetextSearch()
        {
            using (AuthenticationContext.EnterSystemContext())
            {

                // Get the ADO freetext service
                var freetextService = ApplicationServiceContext.Current.GetService<IFreetextSearchService>();
                Assert.IsNotNull(freetextService);

                // Force the rebuild
                var jobManagerService = ApplicationServiceContext.Current.GetService<IJobManagerService>();
                Assert.IsNotNull(jobManagerService);
                var rebuildJob = jobManagerService.GetJobInstance(Guid.Parse(AdoRebuildFreetextIndexJob.JobUuid));
                Assert.IsNotNull(rebuildJob, "Job was not registered");

                // Build
                rebuildJob.Run(this, EventArgs.Empty, new object[0]);

                // Ensure search for name
                var results = freetextService.SearchEntity<Place>(new string[] { "United" });
                Assert.GreaterOrEqual(results.Count(), 2);
                var ordered = results.OrderByDescending(o => o.VersionSequence);
                Assert.Greater(ordered.First().VersionSequence, ordered.Skip(1).First().VersionSequence);


            }
        }
    }
}
