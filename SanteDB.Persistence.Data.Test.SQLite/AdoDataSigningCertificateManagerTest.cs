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
 * Date: 2024-6-21
 */
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    [TestFixture(Category = "SQLite Persistence")]
    [ExcludeFromCodeCoverage]
    public class AdoDataSigningCertificateManagerTest : DataPersistenceTest
    {
        private X509Certificate2 GetCertificate()
        {
            return new X509Certificate2(X509Certificate2.CreateFromCertFile(
                Path.Combine(Path.GetDirectoryName(typeof(AdoCertificateIdentityProviderTest).Assembly.Location),
                "test.lumon.com.cer")));
        }

        /// <summary>
        /// Test that this implementation does not conflict with identity provider and vice versa
        /// </summary>
        [Test]
        public void TestDoesNotConflictWithIdentity()
        {
            var certSignService = ApplicationServiceContext.Current.GetService<IDataSigningCertificateManagerService>();
            Assert.IsNotNull(certSignService);
            var certAuthService = ApplicationServiceContext.Current.GetService<ICertificateIdentityProvider>();
            Assert.IsNotNull(certAuthService);
            var userService = ApplicationServiceContext.Current.GetService<IIdentityProviderService>();
            Assert.IsNotNull(userService);

            // Create a random user
            var userIdentity = userService.CreateIdentity("TEST_ADO_X509_SIG_02", "@TeST123!", AuthenticationContext.SystemPrincipal);
            Assert.IsNotNull(userIdentity);

            // Assign the certificate to the identity
            certSignService.AddSigningCertificate(userIdentity, this.GetCertificate(), AuthenticationContext.SystemPrincipal);

            // Lookup 
            var cert = certSignService.GetSigningCertificates(userIdentity).FirstOrDefault();
            Assert.IsNotNull(cert);
            Assert.AreEqual(this.GetCertificate().Thumbprint, cert.Thumbprint);

            Assert.IsTrue(certSignService.TryGetSigningCertificateByThumbprint(this.GetCertificate().Thumbprint, out cert));
            Assert.IsTrue(certSignService.TryGetSigningCertificateByHash(this.GetCertificate().GetCertHash(), out cert));
            // Ensure that the identity service does not return
            Assert.IsFalse(certAuthService.GetIdentityCertificates(userIdentity).Any());
            try
            {
                certAuthService.Authenticate(this.GetCertificate());
                Assert.Fail("Should have thrown exception");
            }
            catch
            {

            }

            // Test that we can add the certificate for authentication
            certAuthService.AddIdentityMap(userIdentity, this.GetCertificate(), AuthenticationContext.SystemPrincipal);
            certAuthService.Authenticate(this.GetCertificate()); // this line should now execute
            cert = certAuthService.GetIdentityCertificates(userIdentity).FirstOrDefault();
            Assert.IsNotNull(cert);
            cert = certSignService.GetSigningCertificates(userIdentity).FirstOrDefault();
            Assert.IsNotNull(cert);

            // Remove the signing certificate and signing should be gone but auth should work
            certSignService.RemoveSigningCertificate(userIdentity, cert, AuthenticationContext.SystemPrincipal);
            cert = certSignService.GetSigningCertificates(userIdentity).FirstOrDefault();
            Assert.IsNull(cert);
            cert = certAuthService.GetIdentityCertificates(userIdentity).FirstOrDefault();
            Assert.IsNotNull(cert);

            // Clean up
            certAuthService.RemoveIdentityMap(userIdentity, this.GetCertificate(), AuthenticationContext.SystemPrincipal);
        }

        [Test]
        public void TestCanAssignSigningCertificate()
        {
            var certSignService = ApplicationServiceContext.Current.GetService<IDataSigningCertificateManagerService>();
            Assert.IsNotNull(certSignService);
            var userService = ApplicationServiceContext.Current.GetService<IIdentityProviderService>();
            Assert.IsNotNull(userService);

            // Create a random user
            var userIdentity = userService.CreateIdentity("TEST_ADO_X509_SIG_01", "@TeST123!", AuthenticationContext.SystemPrincipal);
            Assert.IsNotNull(userIdentity);

            // Assign the certificate to the identity
            certSignService.AddSigningCertificate(userIdentity, this.GetCertificate(), AuthenticationContext.SystemPrincipal);

            // Lookup 
            var cert = certSignService.GetSigningCertificates(userIdentity).FirstOrDefault();
            Assert.IsNotNull(cert);
            Assert.AreEqual(this.GetCertificate().Thumbprint, cert.Thumbprint);
            certSignService.RemoveSigningCertificate(userIdentity, cert, AuthenticationContext.SystemPrincipal);
            cert = certSignService.GetSigningCertificates(userIdentity).FirstOrDefault();
            Assert.IsNull(cert);
        }
    }
}
