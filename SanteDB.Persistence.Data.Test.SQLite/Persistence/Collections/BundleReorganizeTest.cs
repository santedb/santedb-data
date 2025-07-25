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
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Services.Persistence.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Persistence.Data.Test.SQLite.Persistence.Collections
{
    /// <summary>
    /// Ensures that the bundle service can reorganize itself
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class BundleReorganizeTest
    {


        /// <summary>
        /// Verifies that a bundle with contents A, B, C where:
        /// A relies on B
        /// B relies on C
        /// can be re-organized to bundle C, A, B
        /// </summary>
        [Test]
        public void TestReorganizeSimple()
        {
            IdentifiedData c = new Person() { Key = Guid.NewGuid() },
                b = new Patient() { Key = Guid.NewGuid(), Relationships = new List<EntityRelationship>() { new EntityRelationship(EntityRelationshipTypeKeys.Mother, c as Entity) } },
                a = new Act() { Key = Guid.NewGuid(), Participations = new List<ActParticipation>() { new ActParticipation(ActParticipationKeys.RecordTarget, b as Entity) } };

            var serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            var reorganized = serviceManager.CreateInjected<BundlePersistenceService>().ReorganizeForInsert(new Core.Model.Collection.Bundle(new IdentifiedData[] { a, b, c }));
            Assert.AreEqual(0, reorganized.Item.IndexOf(c));
            Assert.AreEqual(1, reorganized.Item.IndexOf(b));
            Assert.AreEqual(2, reorganized.Item.IndexOf(a));
        }

        /// <summary>
        /// Tests that bundles { A, B, C, D, E, F }, { B, A, D, E, C, F }, { F, E, D, C, B, A } and { E, F, B, A, C, D } where
        /// A relies on B and E
        /// B relies on F
        /// C relies on A
        /// D relies on C, E and A
        /// F relies on E
        /// are all organized to the proper ordering of { E, F, B, A, C, D }
        /// </summary>
        [Test]
        public void TestReorganizeComplex()
        {
            Guid aUuid = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"),
                bUuid = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"),
                cUuid = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"),
                dUuid = Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD"),
                eUuid = Guid.Parse("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE"),
                fUuid = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

            IdentifiedData e = new Organization() { Key = eUuid, Relationships = new List<EntityRelationship>() },
                f = new Person() { Key = fUuid, Relationships = new List<EntityRelationship>() { new EntityRelationship(EntityRelationshipTypeKeys.Employee, e as Entity) } },
                b = new Patient() { Key = bUuid, Relationships = new List<EntityRelationship>() { new EntityRelationship(EntityRelationshipTypeKeys.Mother, f as Entity) } },
                a = new Provider() { Key =aUuid, Relationships = new List<EntityRelationship>() { new EntityRelationship(EntityRelationshipTypeKeys.HealthcareProvider, b as Entity), new EntityRelationship(EntityRelationshipTypeKeys.Employee, e as Entity) } },
                c = new Act() { Key =cUuid, Participations = new List<ActParticipation>() { new ActParticipation(ActParticipationKeys.Admitter, a as Entity) } },
                d = new Act()
                {
                    Key = dUuid,
                    Relationships = new List<ActRelationship>() { new ActRelationship(ActRelationshipTypeKeys.HasComponent, c as Act) },
                    Participations = new List<ActParticipation>()
                {
                    new ActParticipation(ActParticipationKeys.InformationRecipient, e as Entity),
                    new ActParticipation(ActParticipationKeys.Admitter, a as Entity)
                }
                };

            var ror = CollectionUtils.ReorganizeForInsert(new IdentifiedData[] { a, b, c, d, e, f }).ToArray();
            ror = CollectionUtils.ReorganizeForInsert(new IdentifiedData[] { f, a, d, e, b, c }).ToArray();
            ror = CollectionUtils.ReorganizeForInsert(new IdentifiedData[] { a, e, d, b, f, c }).ToArray();

            var serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            var persistenceService = serviceManager.CreateInjected<BundlePersistenceService>();
            var reorganized = persistenceService.ReorganizeForInsert(new Core.Model.Collection.Bundle(new IdentifiedData[] { a, b, c, d, e, f }));
            Assert.AreEqual(0, reorganized.Item.IndexOf(e));
            Assert.AreEqual(1, reorganized.Item.IndexOf(f));
            Assert.AreEqual(2, reorganized.Item.IndexOf(b));
            Assert.AreEqual(3, reorganized.Item.IndexOf(a));
            Assert.AreEqual(4, reorganized.Item.IndexOf(c));
            Assert.AreEqual(5, reorganized.Item.IndexOf(d));

            reorganized = persistenceService.ReorganizeForInsert(new Core.Model.Collection.Bundle(new IdentifiedData[] { b, a, d, e, c, f }));
            Assert.AreEqual(0, reorganized.Item.IndexOf(e));
            Assert.AreEqual(1, reorganized.Item.IndexOf(f));
            Assert.AreEqual(2, reorganized.Item.IndexOf(b));
            Assert.AreEqual(3, reorganized.Item.IndexOf(a));
            Assert.AreEqual(4, reorganized.Item.IndexOf(c));
            Assert.AreEqual(5, reorganized.Item.IndexOf(d));

            reorganized = persistenceService.ReorganizeForInsert(new Core.Model.Collection.Bundle(new IdentifiedData[] { f, e, d, c, b, a }));
            Assert.AreEqual(0, reorganized.Item.IndexOf(e));
            Assert.AreEqual(1, reorganized.Item.IndexOf(f));
            Assert.AreEqual(2, reorganized.Item.IndexOf(b));
            Assert.AreEqual(3, reorganized.Item.IndexOf(a));
            Assert.AreEqual(4, reorganized.Item.IndexOf(c));
            Assert.AreEqual(5, reorganized.Item.IndexOf(d));

            reorganized = persistenceService.ReorganizeForInsert(new Core.Model.Collection.Bundle(new IdentifiedData[] { e, f, b, a, c, d }));
            Assert.AreEqual(0, reorganized.Item.IndexOf(e));
            Assert.AreEqual(1, reorganized.Item.IndexOf(f));
            Assert.AreEqual(2, reorganized.Item.IndexOf(b));
            Assert.AreEqual(3, reorganized.Item.IndexOf(a));
            Assert.AreEqual(4, reorganized.Item.IndexOf(c));
            Assert.AreEqual(5, reorganized.Item.IndexOf(d));

        }
    }

}
