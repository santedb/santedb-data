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
using SanteDB.Core.Data.Import;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    [TestFixture]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class AdoForeignDataManagerTest : DataPersistenceTest
    {

        [Test]
        public void TestCanStageData()
        {

            var patientPersistence = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();
            var identityDomainPersistence = ApplicationServiceContext.Current.GetService<IRepositoryService<IdentityDomain>>();
            var placePersistenceService = ApplicationServiceContext.Current.GetService<IRepositoryService<Place>>();

            using (AuthenticationContext.EnterSystemContext())
            {

                Console.WriteLine("Setting up data...");
                if (!placePersistenceService.Find(o => o.Names.Any(n => n.Component.Any(c => c.Value == "Clinic1"))).Any())
                {
                    placePersistenceService.Insert(new Place()
                    {
                        ClassConceptKey = EntityClassKeys.ServiceDeliveryLocation,
                        Names = new List<EntityName>()
                    {
                        new EntityName(NameUseKeys.OfficialRecord, "Clinic1")
                    }
                    });
                    placePersistenceService.Insert(new Place()
                    {
                        ClassConceptKey = EntityClassKeys.ServiceDeliveryLocation,
                        Names = new List<EntityName>()
                    {
                        new EntityName(NameUseKeys.OfficialRecord, "Hospital1")
                    }
                    });
                    placePersistenceService.Insert(new Place()
                    {
                        ClassConceptKey = EntityClassKeys.ServiceDeliveryLocation,
                        Names = new List<EntityName>()
                    {
                        new EntityName(NameUseKeys.OfficialRecord, "Hospital2")
                    }
                    });
                }
                if (!identityDomainPersistence.Find(o => o.DomainName == "MRN_I").Any())
                {
                    identityDomainPersistence.Insert(new IdentityDomain("MRN_I", "Medical Record Number", "2.25.04949330393"));
                    identityDomainPersistence.Insert(new IdentityDomain("INSURANCE_I", "Insurnace Number", "2.25.9494384383"));
                }

                var foreignDataManager = ApplicationServiceContext.Current.GetService<IForeignDataManagerService>();
                Assert.IsNotNull(foreignDataManager);
                Console.WriteLine("Testing Issue Generation...");

                // Test - cannot find map
                using (var fds = typeof(AdoForeignDataManagerTest).Assembly.GetManifestResourceStream("SanteDB.Persistence.Data.Test.SQLite.Resources.BadPatients.csv"))
                {
                    var fdi = foreignDataManager.Stage(fds, "badpatients.csv", "test", Guid.NewGuid());
                    Assert.AreEqual(1, fdi.Issues.Count());
                    foreignDataManager.Delete(fdi.Key.Value);

                    fds.Seek(0, System.IO.SeekOrigin.Begin);
                    fdi = foreignDataManager.Stage(fds, "badpatients.csv", "test", Guid.Parse("4ABA7190-B975-4623-92A2-7EF105E0C428"));
                    Assert.AreEqual(10, fdi.Issues.Count());


                }

                Console.WriteLine("Testing Stage...");

                using (var fds = typeof(AdoForeignDataManagerTest).Assembly.GetManifestResourceStream("SanteDB.Persistence.Data.Test.SQLite.Resources.Patients.csv"))
                {
                    var fdi = foreignDataManager.Stage(fds, "patients.csv", "test", Guid.Parse("4ABA7190-B975-4623-92A2-7EF105E0C428"));
                    Assert.AreEqual("patients.csv", fdi.Name);
                    Assert.IsNotNull(fdi.Issues);
                    Assert.AreEqual(0, fdi.Issues.Count());
                    Assert.AreEqual(ForeignDataStatus.Scheduled, fdi.Status);
                    var results = foreignDataManager.Find(o => o.Status == ForeignDataStatus.Scheduled);
                    Assert.AreEqual(1, results.Count());
                    Console.WriteLine("Testing Execute...");

                    if (foreignDataManager is IReportProgressChanged irpt)
                        irpt.ProgressChanged += (o, e) => Console.WriteLine(e.State);

                    fdi = foreignDataManager.Execute(results.First().Key.Value);
                    Assert.AreEqual(ForeignDataStatus.CompletedWithErrors, fdi.Status);

                    // Reject Stream can be read

                    var rjs = fdi.GetRejectStream();
                    if (rjs != null)
                    {
                        Console.WriteLine("Testing Rejects...");
                        using (var sr = new StreamReader(fdi.GetRejectStream()))
                        {
                            Assert.DoesNotThrow(() => Console.WriteLine(sr.ReadLine()));
                        }
                    }
                }
            }
        }

    }
}
