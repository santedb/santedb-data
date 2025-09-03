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
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Persistence.Data.Test.SQLite.Persistence.Acts
{
    /// <summary>
    /// Coded observation persistence test
    /// </summary>
    [TestFixture(Category = "SQLite Persistence")]
    [ExcludeFromCodeCoverage]
    public class DateObservationPersistenceTest : DataPersistenceTest
    {

        /// <summary>
        /// Test persistence with proper persistence classes
        /// </summary>
        [Test]
        public void TestPersistWithProper()
        {
            using (AuthenticationContext.EnterSystemContext())
            {

                var dateObservation = new DateObservation()
                {
                    ActTime = DateTimeOffset.Now.Date,
                    MoodConceptKey = ActMoodKeys.Goal,
                    TypeConceptKey = ObservationTypeKeys.Symptom,
                    InterpretationConceptKey = ActInterpretationKeys.AbnormalHigh,
                    Value = DateTime.Today,
                    ValuePrecision = Core.Model.DataTypes.DatePrecision.Day
                };

                var afterInsert = base.TestInsert(dateObservation);

                // Test for querying
                var today = DateTime.Today;
                var yesterday = DateTime.Today.AddDays(-1);
                base.TestQuery<DateObservation>(o => o.Value == today, 1);
                var obj = base.TestQuery<Observation>(o => o.TypeConceptKey == ObservationTypeKeys.Symptom, 1).First();
                Assert.IsInstanceOf<DateObservation>(obj);
                base.TestQuery<DateObservation>(o => o.Value == yesterday, 0);
                var afterQuery = base.TestQuery<DateObservation>(o => o.Value == today && o.TypeConceptKey == ObservationTypeKeys.Symptom, 1).First();
                Assert.AreEqual(DatePrecision.Day, afterQuery.ValuePrecision);
                base.TestQuery<DateObservation>(o => o.Value == today, 1).First();

                // Test update
                var afterUpdate = base.TestUpdate(afterQuery, o =>
                {
                    o.Value = yesterday;
                    o.ValuePrecision = DatePrecision.Year;
                    return o;
                });
                Assert.AreEqual(yesterday, afterUpdate.Value);
                Assert.AreEqual(DatePrecision.Year, afterUpdate.ValuePrecision);
                Assert.AreEqual(today, (afterUpdate.GetPreviousVersion() as DateObservation).Value);

                // Delete
                base.TestDelete(afterInsert, Core.Services.DeleteMode.LogicalDelete);
                base.TestQuery<DateObservation>(o => o.Value == yesterday, 0);
                base.TestQuery<DateObservation>(o => o.Value == yesterday && o.ObsoletionTime != null, 1);

                // Un-delete
                base.TestUpdate(afterQuery, o =>
                {
                    return o;
                });
                base.TestQuery<DateObservation>(o => o.Value == yesterday, 1);
                base.TestQuery<DateObservation>(o => o.Value == yesterday && o.ObsoletionTime != null, 0);

                // Test perma delete
                base.TestDelete(afterInsert, Core.Services.DeleteMode.PermanentDelete);
                base.TestQuery<DateObservation>(o => o.Value == yesterday, 0);
                base.TestQuery<DateObservation>(o => o.Value == yesterday && o.ObsoletionTime != null, 0);

                // should fail on update
                try
                {
                    base.TestUpdate(afterQuery, o =>
                    {
                        return o;
                    });
                    Assert.Fail("Should have thrown exception");
                }
                catch (DataPersistenceException e) when (e.InnerException is KeyNotFoundException k) { }
                catch { Assert.Fail("Wrong exception type thrown"); }
            }
        }
    }

}
