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
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System.Linq;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    [TestFixture]
    public class AdoRelationshipValidationTest : DataPersistenceTest
    {

        [Test]
        public void TestCanQueryRelationshipValidationRule()
        {

            var perstest = ApplicationServiceContext.Current.GetService<IRelationshipValidationProvider>();
            Assert.IsNotNull(perstest);
            Assert.AreNotEqual(0, perstest.QueryRelationships(o => true).Count());
            Assert.AreNotEqual(0, perstest.QueryRelationships(o => o.SourceClassKey == EntityClassKeys.Patient).Count());
        }

        [Test]
        public void TestCanRegisterRelationshipValidationRule()
        {
            var perstest = ApplicationServiceContext.Current.GetService<IRelationshipValidationProvider>();
            Assert.IsNotNull(perstest);

            using (AuthenticationContext.EnterSystemContext())
            {
                var retVal = perstest.AddValidRelationship<EntityRelationship>(EntityClassKeys.Patient, EntityClassKeys.Material, EntityRelationshipTypeKeys.QualifiedEntity, "A test relationship");
                Assert.AreEqual(EntityClassKeys.Patient, retVal.SourceClassKey);
                Assert.AreEqual(EntityClassKeys.Material, retVal.TargetClassKey);
                Assert.AreEqual(EntityRelationshipTypeKeys.QualifiedEntity, retVal.RelationshipTypeKey);
                Assert.AreEqual(1, perstest.QueryRelationships(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.QualifiedEntity && o.SourceClassKey == EntityClassKeys.Patient && o.TargetClassKey == EntityClassKeys.Material).Count());
                Assert.IsTrue(perstest.GetValidRelationships<EntityRelationship>(EntityClassKeys.Patient).Any(o => o.TargetClassKey == EntityClassKeys.Material));

                perstest.RemoveRuleByKey(retVal.Key.Value);
                Assert.AreEqual(0, perstest.QueryRelationships(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.QualifiedEntity && o.SourceClassKey == EntityClassKeys.Patient && o.TargetClassKey == EntityClassKeys.Material).Count());
                Assert.IsFalse(perstest.GetValidRelationships<EntityRelationship>(EntityClassKeys.Patient).Any(o => o.TargetClassKey == EntityClassKeys.Material));
            }


        }

    }
}
