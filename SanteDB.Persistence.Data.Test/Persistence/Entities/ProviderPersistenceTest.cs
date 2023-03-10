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
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Persistence.Data.Test.Persistence.Entities
{
    /// <summary>
    /// User entity persistence test
    /// </summary>
    [TestFixture(Category = "Persistence")]
    [ExcludeFromCodeCoverage]
    public class ProviderPersistenceTest : DataPersistenceTest
    {

        /// <summary>
        /// Ensures that the persistence of a provider entity with the proper persistence service
        /// </summary>
        [Test]
        public void TestInsertWithProper()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                var provider = new Provider()
                {
                    Addresses = new List<EntityAddress>()
                    {
                        new EntityAddress(AddressUseKeys.HomeAddress, "123 Main Street East", "Hamilton", "ON", "CA", "L8K5N2")
                    },
                    Names = new List<EntityName>()
                    {
                        new EntityName(NameUseKeys.Legal, "Test", "Provider")
                    },
                    LanguageCommunication = new List<PersonLanguageCommunication>()
                    {
                        new PersonLanguageCommunication("en", true)
                    },
                    Specialty = new Concept()
                    {
                        Mnemonic = "Specialty-Test",
                        ClassKey = ConceptClassKeys.Other,
                        StatusConceptKey = StatusKeys.Active
                    },
                    DateOfBirth = new DateTime(1984, 05, 04)
                };

                // Insert user entity
                var afterInsert = base.TestInsert(provider);
                Assert.IsInstanceOf<Provider>(provider);
                Assert.IsNull(afterInsert.Specialty);
                Assert.AreEqual("Specialty-Test", afterInsert.LoadProperty(o => o.Specialty).Mnemonic);

                // Attempt lookup by user name
                var afterQuery = base.TestQuery<Provider>(o => o.Specialty.Mnemonic == "Specialty-Test", 1).AsResultSet().First();
                Assert.AreEqual(afterQuery.Key, afterInsert.Key);
                Assert.AreEqual(1, afterQuery.LoadProperty(o => o.LanguageCommunication).Count);
                Assert.IsNull(afterQuery.Specialty);
                Assert.AreEqual("Specialty-Test", afterQuery.LoadProperty(o => o.Specialty).Mnemonic);

            }

        }

    }
}
