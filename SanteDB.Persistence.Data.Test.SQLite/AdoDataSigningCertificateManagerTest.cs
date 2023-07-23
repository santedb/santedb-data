using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

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
            Assert.IsNull(certAuthService.GetIdentityCertificates(userIdentity));
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
