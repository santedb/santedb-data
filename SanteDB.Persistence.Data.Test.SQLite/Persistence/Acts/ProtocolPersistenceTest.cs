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
 * Date: 2023-5-19
 */
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Cdss;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using SanteDB.Matcher.Matchers;
using SanteDB.Core.Model;

namespace SanteDB.Persistence.Data.Test.SQLite.Persistence.Acts
{
    /// <summary>
    /// A test for protocol persistence
    /// </summary>
    [TestFixture()]
    [ExcludeFromCodeCoverage]
    public class ProtocolPersistenceTest : DataPersistenceTest
    {

        /// <summary>
        /// CDSS library for testing
        /// </summary>
        private class DummyCdssLibrary : ICdssLibrary
        {
            private ICdssProtocol m_protocol;

            public DummyCdssLibrary()
            {

            }

            public DummyCdssLibrary(Protocol protocol)
            {
                this.m_protocol = new DummyCdssProtocol(protocol);
            }

            /// <summary>
            /// Gets the protocols
            /// </summary>
            public IEnumerable<ICdssProtocol> GetProtocols(String forType)
            {
                yield return this.m_protocol;
            }

            public Guid Uuid => Guid.NewGuid();

            public string Id => "123-SAMPLE";

            public string Name => "Sample Protocol";

            public string Version => "1.2";

            public string Oid => "2.25.3029329232";

            public string Documentation => "Some demonstration protocol";

            public IEnumerable<DetectedIssue> Analyze(IdentifiedData analysisTarget, IDictionary<string, object> parameters)
            {
                yield break;
            }

            public IEnumerable<object> Execute(IdentifiedData target, IDictionary<string, object> parameters)
            {
                yield break;
            }


            public void Load(Stream definitionStream)
            {
                Assert.AreEqual(5, definitionStream.Read(new byte[5], 0, 5));
            }

            public void Save(Stream definitionStream)
            {
                definitionStream.Write(new byte[] { 0, 1, 2, 3, 4 }, 0, 5);
            }
        }

        /// <summary>
        /// Protocol handler class
        /// </summary>
        [ExcludeFromCodeCoverage]
        private class DummyCdssProtocol : ICdssProtocol
        {

            // Protocol
            private Protocol m_protocol;

            public Guid Uuid => this.m_protocol.Key.Value;

            public string Name => this.m_protocol.Name;

            public string Version => "1.0";
            public string Oid => "2.25.340403403434234234234232423";

            public string Id => "SAMPLE";

            public string Documentation => "THIS IS AN EXAMPLE";

            public IEnumerable<ICdssProtocolScope> Scopes => new ICdssProtocolScope[0];

            public IEnumerable<Act> ComputeProposals(Patient p, IDictionary<string, object> parameters)
            {
                return new Act[0];
            }


            public DummyCdssProtocol()
            {

            }

            public DummyCdssProtocol(Protocol protocol)
            {
                this.m_protocol = protocol;
            }

            public Protocol GetProtocolData()
            {
                return this.m_protocol;
            }

            public ICdssProtocol Load(Protocol protocolData)
            {
                this.m_protocol = protocolData;
                return this;
            }

            public void Prepare(Patient p, IDictionary<string, object> parameters)
            {
                ;
            }

        }

        /// <summary>
        /// Test that new protocols can be persisted, and then loaded back from the protocol persistence service
        /// </summary>
        [Test]
        public void TestPersistProtocol()
        {
            // First we create a protocol
            var protocol = new Protocol()
            {
                Oid = "1.2.3.4.5.6",
                Name = "Teapot Protocol"
            };

            using (AuthenticationContext.EnterSystemContext())
            {

                // Insert the protocol
                var afterInsert = base.TestInsert(protocol);
                Assert.AreEqual(AuthenticationContext.SystemUserSid, afterInsert.CreatedByKey.Value.ToString());
                Assert.AreEqual("1.2.3.4.5.6", afterInsert.Oid);

                // Test querying the protocol
                var afterQuery = base.TestQuery<Protocol>(o => o.Oid == "1.2.3.4.5.6", 1).AsResultSet().First();
                base.TestQuery<Protocol>(o => o.Oid == "1.2.3.4.5.76", 0);
                base.TestQuery<Protocol>(o => o.ObsoletionTime == null && o.Name == "Teapot Protocol", 1);
                Assert.AreEqual(AuthenticationContext.SystemUserSid, afterInsert.CreatedByKey.Value.ToString());
                Assert.AreEqual("1.2.3.4.5.6", afterQuery.Oid);

                // Test the update of a protocol
                var afterUpdate = base.TestUpdate(afterQuery, o =>
                {
                    o.Name = "Non-Teapot Protocol";
                    o.Oid = "6.5.4.3.2.1";
                    return o;
                });
                Assert.AreEqual("Non-Teapot Protocol", afterUpdate.Name);

                // Validate query
                base.TestQuery<Protocol>(o => o.Oid == "1.2.3.4.5.6", 0);
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1", 1);

                // Delete 
                base.TestDelete(afterUpdate, Core.Services.DeleteMode.LogicalDelete);
                // Validate delete
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1", 0);
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1" && o.ObsoletionTime != null, 1);

                // Un-delete
                base.TestUpdate(afterQuery, o => o);
                // Validate un-delete
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1", 1);
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1" && o.ObsoletionTime != null, 0);

                // Perma delete
                base.TestDelete(afterUpdate, Core.Services.DeleteMode.PermanentDelete);
                // Validate delete
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1", 0);
                base.TestQuery<Protocol>(o => o.Oid == "6.5.4.3.2.1" && o.ObsoletionTime != null, 0);

                // Validate non-un-delete
                try
                {
                    // Un-delete
                    base.TestUpdate(afterQuery, o => o);
                    Assert.Fail("Should have thrown exception");
                }
                catch (DataPersistenceException e) when (e.InnerException is KeyNotFoundException)
                {

                }
                catch
                {
                    Assert.Fail("Wrong type of exception thrown!");
                }

            }
        }

        /// <summary>
        /// Test the clinical protocol repository functions work for converting data to/from database to the CDSS execution environment
        /// </summary>
        [Test]
        public void TestClinicalProtocolRepository()
        {
            // First we create a protocol
            var protocol = new Protocol()
            {
                Oid = "1.2.3.4.5.7",
                Name = "Teapot Protocol 2"
            };

            using (AuthenticationContext.EnterSystemContext())
            {

                // We want to create the IClinicalProtocol instance
                var tde = new DummyCdssLibrary(protocol);
                var service = ApplicationServiceContext.Current.GetService<ICdssLibraryRepository>();
                Assert.IsNotNull(service);

                var afterInsert = service.InsertOrUpdate(tde);
                Assert.AreEqual(tde.Name, afterInsert.Name);

                // Now attempt to load
                var afterGet = service.Get(afterInsert.Uuid);
                Assert.AreEqual(tde.Name, afterGet.Name);

                // Attempt to search
                var afterSearch = service.Find(o => o.Name == "Teapot Protocol 2");
                Assert.AreEqual(1, afterSearch.Count());
                Assert.AreEqual(tde.Name, afterSearch.First().Name);
            }
        }
    }
}
