﻿using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    /// <summary>
    /// Foreign data importer test needs the database to insert into database
    /// </summary>
    public class ForeignDataImportTest : DataPersistenceTest
    {

        /// <summary>
        /// Test import of 500 patients
        /// </summary>
        [Test]
        public void TestCanImportPatients()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                var patientPersistence = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();
                var identityDomainPersistence = ApplicationServiceContext.Current.GetService<IRepositoryService<IdentityDomain>>();
                var placePersistenceService = ApplicationServiceContext.Current.GetService<IRepositoryService<Place>>();

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
                identityDomainPersistence.Insert(new IdentityDomain("MRN_I", "Medical Record Number", "2.25.04949330393"));
                identityDomainPersistence.Insert(new IdentityDomain("INSURANCE_I", "Insurnace Number", "2.25.9494384383"));

                var serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
                var beforePatientCount = patientPersistence.Find(o => o.ObsoletionTime == null).Count();

                var dataImporter = serviceManager.CreateInjected<DefaultForeignDataImporter>();
                dataImporter.ProgressChanged += (o, e) => Debug.WriteLine(e.State);

                // Load the foreign data map
                using (var definitionStream = typeof(ForeignDataImportTest).Assembly.GetManifestResourceStream("SanteDB.Persistence.Data.Test.SQLite.Resources.SimpleDataMap.xml"))
                {
                    var fdm = ForeignDataMap.Load(definitionStream).Maps.First() ;
                    Assert.IsNotNull(fdm);
                    Assert.IsTrue(ForeignDataImportUtil.Current.TryGetDataFormat("csv", out var foreignDataFormat));
                    using (var rejectStream = new MemoryStream())
                    using (var csvStream = typeof(ForeignDataImportTest).Assembly.GetManifestResourceStream("SanteDB.Persistence.Data.Test.SQLite.Resources.Patients.csv"))
                    {
                        using (var rejectFile = foreignDataFormat.Open(rejectStream))
                        using (var fdFile = foreignDataFormat.Open(csvStream))
                        {
                            using (var rejectWriter = rejectFile.CreateWriter())
                            using (var fdReader = fdFile.CreateReader())
                            {

                                Assert.IsFalse(dataImporter.Validate(fdm, fdReader).Any());
                                var detectedIssues = dataImporter.Import(fdm, fdReader, rejectWriter, TransactionMode.Commit).ToList();
                                Assert.AreEqual(rejectWriter.RecordsWritten, detectedIssues.Count);
                                Assert.AreEqual(beforePatientCount + 100 - detectedIssues.Count, patientPersistence.Find(o => o.ObsoletionTime == null).Count());

                                // Attempt to search and ensure that the patient was imported correctly
                                // 1601850032,10/15/1986 0:00,F,Adams,Kayla,Jennifer,993670-011530-1986A,31 Cannon St. S,Stoney Creek,ON,CA,A2B-3Q2,Hospital1,,Adams,Zoe,
                                var patientOfInterest = patientPersistence.Find(o => o.Identifiers.Any(i => i.Value == "1601850032")).First();
                                Assert.IsNotNull(patientOfInterest);
                                Assert.AreEqual(AdministrativeGenderConceptKeys.Female, patientOfInterest.GenderConceptKey);
                                Assert.AreEqual(new DateTime(1986, 10, 15), patientOfInterest.DateOfBirth);
                                Assert.AreEqual(1, patientOfInterest.LoadProperty(o => o.Names).Count); // no alias 
                                Assert.AreEqual("Adams", patientOfInterest.Names[0].LoadProperty(o => o.Component).First().Value);
                                Assert.AreEqual(2, patientOfInterest.LoadProperty(o => o.Identifiers).Count);

                            }
                        }
                    }
                }
            }
        }

    }
}