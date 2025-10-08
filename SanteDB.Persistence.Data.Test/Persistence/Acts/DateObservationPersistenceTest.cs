using DocumentFormat.OpenXml.Wordprocessing;
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
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Persistence.Data.Test.Persistence.Acts
{
    [TestFixture(Category = "SQLite Persistence")]
    [ExcludeFromCodeCoverage]
    internal class DateObservationPersistenceTest : DataPersistenceTest
    {

        /// <summary>
        /// Test persistence with proper persistence classes
        /// </summary>
        [Test]
        public void TestPersistWithProper()
        {
            using (AuthenticationContext.EnterSystemContext())
            {

                var testDate = DateTime.Now;
                var dateObservation = new DateObservation()
                {
                    ActTime = DateTimeOffset.Now.Date,
                    MoodConceptKey = ActMoodKeys.Goal,
                    TypeConceptKey = ObservationTypeKeys.Symptom,
                    InterpretationConceptKey = ActInterpretationKeys.AbnormalHigh,
                    Value = testDate,
                    ValuePrecision = Core.Model.DataTypes.DatePrecision.Day
                };

                var afterInsert = base.TestInsert(dateObservation);
                
                // Test for querying
                var yesterday = testDate.AddDays(-1);
                base.TestQuery<DateObservation>(o => o.Value == testDate, 1);
                var obj = base.TestQuery<Observation>(o => o.TypeConceptKey == ObservationTypeKeys.Symptom, 1).First();
                Assert.IsInstanceOf<DateObservation>(obj);
                base.TestQuery<DateObservation>(o => o.Value == yesterday, 0);
                var afterQuery = base.TestQuery<DateObservation>(o => o.Value == testDate && o.TypeConceptKey == ObservationTypeKeys.Symptom, 1).First();
                Assert.AreEqual(DatePrecision.Day, afterQuery.ValuePrecision);
                base.TestQuery<DateObservation>(o => o.Value == testDate, 1).First();

                // Test update
                var afterUpdate = base.TestUpdate(afterQuery, o =>
                {
                    o.Value = yesterday;
                    o.ValuePrecision = DatePrecision.Year;
                    return o;
                });
                Assert.AreEqual(yesterday, afterUpdate.Value);
                Assert.AreEqual(DatePrecision.Year, afterUpdate.ValuePrecision);
                Assert.AreEqual(testDate.Date, (afterUpdate.GetPreviousVersion() as DateObservation).Value?.Date);

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
