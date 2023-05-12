using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Persistence.Data.Services.Persistence.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
