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
 */
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Persistence.Data.Test
{
    /// <summary>
    /// Test the subscription implementation executes subscriptions properly
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class AdoSubscriptionExecutorTest : DataPersistenceTest
    {


        /// <summary>
        /// Tets that the subscription logic performs subscriptions by key
        /// </summary>
        [Test]
        public void TestDoesExecuteSubscriptionByKey()
        {
            using (AuthenticationContext.EnterSystemContext())
            {

                var executor = ApplicationServiceContext.Current.GetService<ISubscriptionExecutor>();
                Assert.IsNotNull(executor);

                var parameters = new NameValueCollection();
                parameters.Add("_creationDate", "2022-01-01");

                // Subscription executor should throw exception for unknown subscription
                Assert.Throws<KeyNotFoundException>(() => executor.Execute(Guid.NewGuid(), parameters));

                // Subscription executor should return query result set
                var resultSet = executor.Execute(Guid.Parse(TestSubscriptionRepository.AllEntitiesCreatedAfterDateParm), parameters);
                Assert.IsInstanceOf<IQueryResultSet<Entity>>(resultSet);
                Assert.Greater(resultSet.Count(), 0); // Should be more than one result
                var first = resultSet.FirstOrDefault() as Entity;
                Assert.IsNotNull(first);
                var second = resultSet.Skip(1).FirstOrDefault() as Entity;
                Assert.IsNotNull(second);
                Assert.Less(second.VersionSequence, first.VersionSequence);

                Assert.AreEqual(10, resultSet.Take(10).Count());
                Assert.AreEqual(5, resultSet.Skip(5).Take(5).Count());

            }
        }

        /// <summary>
        /// Test that the subscription system does continue and sort
        /// </summary>
        [Test]
        public void TestDoesContinueAndSort()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                var cities = new string[]
                {
                    "Evenville",
                    "Oddville"
                };

                Enumerable.Range(0, 10).ToList().ForEach(o =>
                {
                    Entity toCreate = null;
                    if (o % 4 == 0)
                    {
                        toCreate = new Patient();
                    }
                    else
                    {
                        toCreate = new Person();
                    }
                    toCreate.Names = new List<EntityName>()
                    {
                        new EntityName(NameUseKeys.Legal, "Smith", $"Jenny John {o}")
                    };
                    toCreate.Addresses = new List<EntityAddress>()
                    {
                        new EntityAddress(AddressUseKeys.PhysicalVisit, "123 Main Street", cities[o%2], "ON", "CA", "123123")
                    };

                    base.TestInsert(toCreate);
                });

                base.TestQuery<Person>(o => o.Addresses.Any(a => a.Component.Any(c => c.ComponentTypeKey == AddressComponentKeys.City && cities.Contains(c.Value))), 10);

                // We should now execute
                var executor = ApplicationServiceContext.Current.GetService<ISubscriptionExecutor>();
                Assert.IsNotNull(executor);

                var parameters = new NameValueCollection();
                parameters.Add("_city", "Evenville");

                // Execute subscription
                var resultSet = executor.Execute(Guid.Parse(TestSubscriptionRepository.AllPatientsOrPersonsEntitiesInCity), parameters) as IQueryResultSet<Entity>;
                Assert.IsInstanceOf<IQueryResultSet<Entity>>(resultSet);
                Assert.AreEqual(5, resultSet.Count());

                // Now try to sort 
                var sorted = resultSet.OrderBy(o => o.VersionSequence);
                Assert.Greater(sorted.Skip(1).First().VersionSequence, sorted.First().VersionSequence);
                var descSorted = resultSet.OrderByDescending(o => o.VersionSequence);
                Assert.Greater(descSorted.First().VersionSequence, descSorted.Skip(1).First().VersionSequence);
                var res = resultSet.ToArray();
                Assert.Greater(sorted.Skip(1).First().VersionSequence, sorted.First().VersionSequence);
                Assert.IsTrue(resultSet.All(o => o.GetType() == typeof(Patient) || o.GetType() == typeof(Person)));
                Assert.IsFalse(resultSet.All(o => o.GetType() == typeof(Patient)));
                Assert.IsFalse(resultSet.All(o => o.GetType() == typeof(Person)));

                // Now multiple parameters
                parameters.Add("_city", "Oddville");
                resultSet = executor.Execute(Guid.Parse(TestSubscriptionRepository.AllPatientsOrPersonsEntitiesInCity), parameters) as IQueryResultSet<Entity>;
                Assert.AreEqual(10, resultSet.Count());

            }
        }

    }
}
