using SanteDB.Core.Security.Principal;
using SanteDB.Persistence.Data.Model.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SanteDB.Persistence.Data.Security
{
    /// <summary>
    /// ADO Device identity creatd with a certificate
    /// </summary>
    internal class AdoDeviceCertificateIdentity : AdoDeviceIdentity, ICertificateIdentity
    {
        /// <inheritdoc/>
        internal AdoDeviceCertificateIdentity(DbSecurityDevice device, X509Certificate2 authenticationCertificate) : base(device, "X.509")
        {
            this.AuthenticationCertificate = authenticationCertificate;
        }

        /// <summary>
        /// Get the authentication certificate
        /// </summary>
        public X509Certificate2 AuthenticationCertificate { get; }
    }
}
