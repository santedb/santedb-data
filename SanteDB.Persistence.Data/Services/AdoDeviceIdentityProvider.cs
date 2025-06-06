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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// An implementation of the device identity provider
    /// </summary>
    public class AdoDeviceIdentityProvider : IDeviceIdentityProviderService, INotifyDeviceIdentityProviderService, ILocalServiceProvider<IDeviceIdentityProviderService>
    {

        /// <inheritdoc/>
        public IDeviceIdentityProviderService LocalProvider => this;


        // Secret claims which should not be disclosed in an IIdentity 
        private readonly String[] m_nonIdentityClaims =
        {
            SanteDBClaimTypes.SanteDBOTAuthCode
        };

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Databased Device Identity Provider";

        /// <summary>
        /// Fired after authenticating a device principal is complete
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Fired prior to authenticating a device principal
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        /// <inheritdoc/>
        public event EventHandler<IdentityEventArgs> Locked;
        /// <inheritdoc/>
        public event EventHandler<IdentityEventArgs> Unlocked;
        /// <inheritdoc/>
        public event EventHandler<IdentityEventArgs> Changed;
        /// <inheritdoc/>
        public event EventHandler<IdentityEventArgs> Created;
        /// <inheritdoc/>
        public event EventHandler<IdentityEventArgs> Deleted;
        /// <inheritdoc/>
        public event EventHandler<IdentityClaimEventArgs> ClaimAdded;
        /// <inheritdoc/>
        public event EventHandler<IdentityClaimEventArgs> ClaimRemoved;

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoDeviceIdentityProvider));

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;

        // Security configuration
        private readonly SecurityConfigurationSection m_securityConfiguration;

        // Pep service
        private readonly IPolicyEnforcementService m_pepService;

        // Localization service
        private readonly ILocalizationService m_localizationService;

        // Hasher
        private IPasswordHashingService m_hasher;

        /// <summary>
        /// Creates a new application identity provider
        /// </summary>
        public AdoDeviceIdentityProvider(IConfigurationManager configurationManager,
            IPasswordHashingService hashingService,
            ILocalizationService localizationService,
            IPolicyEnforcementService pepService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_hasher = hashingService;
            this.m_pepService = pepService;
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Authenticates the specified device identity
        /// </summary>
        /// <param name="deviceId">The public identifier of the device</param>
        /// <param name="deviceSecret">The secret of the device , either a plantext secret or a certificate thumbprint</param>
        /// <param name="authMethod">The authentication method used</param>
        /// <returns></returns>
        public IPrincipal Authenticate(string deviceId, string deviceSecret, AuthenticationMethod authMethod = AuthenticationMethod.Any)
        {
            if (String.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException(nameof(deviceId), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (String.IsNullOrEmpty(deviceSecret))
            {
                throw new ArgumentNullException(nameof(deviceSecret), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (!authMethod.HasFlag(AuthenticationMethod.Local))
            {
                throw new NotSupportedException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_DEV_LOCAL_ONLY_SUPPORTED));
            }

            var preAuthArgs = new AuthenticatingEventArgs(deviceId);
            this.Authenticating?.Invoke(this, preAuthArgs);
            if (preAuthArgs.Cancel)
            {
                if (preAuthArgs.Success)
                {
                    return preAuthArgs.Principal;
                }
                else
                {
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();
                    var dev = context.FirstOrDefault<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == deviceId.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dev == null)
                    {
                        throw new InvalidIdentityAuthenticationException();
                    }

                    if (dev.Lockout.GetValueOrDefault() > DateTimeOffset.Now)
                    {
                        throw new LockedIdentityAuthenticationException(dev.Lockout.Value);
                    }

                    // Peppered authentication
                    var pepperSecret = this.m_configuration.GetPepperCombos(deviceSecret).Select(o => this.m_hasher.ComputeHash(o));
                    // Pepper authentication
                    if (!context.Any<DbSecurityDevice>(a => a.PublicId.ToLowerInvariant() == deviceId.ToLower() && pepperSecret.Contains(a.DeviceSecret)))
                    {
                        dev.InvalidAuthAttempts = dev.InvalidAuthAttempts.GetValueOrDefault() + 1;
                        if (dev.InvalidAuthAttempts > this.m_securityConfiguration.GetSecurityPolicy<Int32>(SecurityPolicyIdentification.MaxInvalidLogins))
                        {
                            var lockoutSlide = 30 * dev.InvalidAuthAttempts.Value;
                            if (DateTimeOffset.Now < DateTimeOffset.MaxValue.AddSeconds(-lockoutSlide))
                            {
                                dev.Lockout = DateTimeOffset.Now.AddSeconds(lockoutSlide);
                            }
                        }
                        context.Update(dev);
                        throw new InvalidIdentityAuthenticationException();
                    }

                    dev.LastAuthentication = DateTimeOffset.Now;

                    dev.DeviceSecret = this.m_hasher.ComputeHash(this.m_configuration.AddPepper(deviceSecret));

                    var identity = new AdoDeviceIdentity(dev, "LOCAL");
                    var retVal = new AdoClaimsPrincipal(identity);

                    var dbClaims = context.Query<DbDeviceClaim>(o => o.SourceKey == dev.Key &&
                           (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now));
                    identity.AddClaims(dbClaims.ToArray().Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));
                    // demand login
                    this.m_pepService.Demand(PermissionPolicyIdentifiers.LoginAsService, retVal);

                    context.Update(dev);

                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, retVal, true));
                    return retVal;
                }
                catch (LockedIdentityAuthenticationException e)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, null, false));
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_DEV_LOCKED, new { time = e.TimeLockExpires }));
                }
                catch (InvalidIdentityAuthenticationException)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, null, false));
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_DEV_INVALID));
                }
                catch (AuthenticationException)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, null, false));
                    throw;
                }
                catch (Exception e)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, null, false));
                    this.m_tracer.TraceError("Could not authenticated device {0}- {1]", deviceId, e);
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_DEV_GENERAL), e);
                }
            }
        }

        /// <summary>
        /// Chenge the device principal secret
        /// </summary>
        public void ChangeSecret(string name, string deviceSecret, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (String.IsNullOrEmpty(deviceSecret))
            {
                throw new ArgumentNullException(nameof(deviceSecret), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (!principal.Identity.IsAuthenticated || !principal.Identity.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    var dev = context.FirstOrDefault<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == name.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dev == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND));
                    }

                    dev.DeviceSecret = this.m_hasher.ComputeHash(this.m_configuration.AddPepper(deviceSecret));
                    dev.UpdatedByKey = context.EstablishProvenance(principal, null);
                    dev.UpdatedTime = DateTimeOffset.Now;
                    context.Update(dev);

                    this.Changed?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dev), principal));
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error updating secret for device {0} - {1}", name, e);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.UPDATE_SECRET), e);
                }
            }
        }

        /// <inheritdoc/>
        public IDeviceIdentity GetIdentity(Guid sid)
        {
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open();
                    var dev = context.FirstOrDefault<DbSecurityDevice>(d => d.Key == sid && d.ObsoletionTime == null);
                    if (dev == null)
                    {
                        return null;
                    }
                    var dbClaims = context.Query<DbDeviceClaim>(o => o.SourceKey == dev.Key &&
                       (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now));
                    var retVal = new AdoDeviceIdentity(dev);
                    retVal.AddClaims(dbClaims.ToArray().Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));
                    return retVal;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching identity for {0}", sid);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FETCH_DEVICE_KEY), e);
                }
            }
        }

        /// <summary>
        /// Gets an unauthenticated device identity for the specified name
        /// </summary>
        public IDeviceIdentity GetIdentity(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open();
                    var dev = context.FirstOrDefault<DbSecurityDevice>(d => d.PublicId.ToLowerInvariant() == name.ToLowerInvariant() && d.ObsoletionTime == null);
                    if (dev == null)
                    {
                        return null;
                    }

                    var dbClaims = context.Query<DbDeviceClaim>(o => o.SourceKey == dev.Key &&
                       (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now));
                    var retVal = new AdoDeviceIdentity(dev);
                    retVal.AddClaims(dbClaims.ToArray().Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));
                    return retVal;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching identity for {0}", name);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FETCH_DEVICE_KEY), e);
                }
            }
        }

        /// <summary>
        /// Sets the lockout state of the device
        /// </summary>
        public void SetLockout(string name, bool lockoutState, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (!principal.Identity.IsAuthenticated || principal != AuthenticationContext.SystemPrincipal)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);
            }

            // Get the write connection
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();
                    var dev = context.FirstOrDefault<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == name.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dev == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND));
                    }

                    if (lockoutState)
                    {
                        dev.Lockout = DateTimeOffset.MaxValue.ToLocalTime();
                    }
                    else
                    {
                        dev.Lockout = null;
                        dev.LockoutSpecified = true;
                    }

                    dev = context.Update(dev);
                    if (lockoutState)
                    {
                        this.Locked?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dev), principal));
                    }
                    else
                    {
                        this.Unlocked?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dev), principal));
                    }

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error updating lockout state for {0}", name);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SET_LOCKOUT), e);
                }
            }
        }

        /// <summary>
        /// Create a new device identity
        /// </summary>
        public IDeviceIdentity CreateIdentity(string deviceId, string secret, IPrincipal principal, Guid? withSid = null)
        {
            if (String.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException(nameof(deviceId), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(secret))
            {
                throw new ArgumentNullException(nameof(secret), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice, principal);

            // Open context
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {
                        // Is password null? If so generate new
                        if (String.IsNullOrEmpty(secret))
                        {
                            secret = BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", "");
                        }

                        // Create the principal
                        DbSecurityDevice dbsa = new DbSecurityDevice()
                        {
                            Key = withSid ?? Guid.NewGuid(),
                            PublicId = deviceId,
                            CreatedByKey = context.EstablishProvenance(principal, null),
                            CreationTime = DateTimeOffset.Now,
                            DeviceSecret = this.m_hasher.ComputeHash(this.m_configuration.AddPepper(secret))
                        };
                        dbsa = context.Insert(dbsa);

                        // Assign default group policies
                        var skelSql = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSecurityRolePolicy))
                            .InnerJoin<DbSecurityRolePolicy, DbSecurityRole>(o => o.SourceKey, o => o.Key)
                            .Where<DbSecurityRole>(o => o.Name == "DEVICE")
                            .Statement;

                        context.Query<DbSecurityRolePolicy>(skelSql)
                            .ToList()
                            .ForEach(o => context.Insert(new DbSecurityDevicePolicy()
                            {
                                GrantType = o.GrantType,
                                PolicyKey = o.PolicyKey,
                                SourceKey = dbsa.Key,
                                Key = Guid.NewGuid()
                            }));

                        tx.Commit();

                        this.Created?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dbsa), principal));
                        return new AdoDeviceIdentity(dbsa);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error creating device identity {0} : {1}", deviceId, e);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_DEV_CREATE), e);
                }
            }
        }

        /// <summary>
        /// Delete identity
        /// </summary>
        public void DeleteIdentity(string name, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice, principal);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    var dbDev = context.FirstOrDefault<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == name.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbDev == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.FETCH_DEVICE_KEY));
                    }

                    dbDev.ObsoletedByKey = context.EstablishProvenance(principal, null);
                    dbDev.ObsoletionTime = DateTimeOffset.Now;
                    context.Update(dbDev);
                    this.Deleted?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dbDev), principal));
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DEV_DELETE_ERROR), e);
                }
            }
        }

        /// <summary>
        /// Get the security identifier
        /// </summary>
        public Guid GetSid(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open();
                    return context.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == name.ToLowerInvariant() && o.ObsoletionTime == null).Select(o => o.Key).FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
                }
            }
        }


        /// <inheritdoc/>
        public void AddClaim(string deviceName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if (String.IsNullOrEmpty(deviceName))
            {
                throw new ArgumentNullException(nameof(deviceName));
            }
            else if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice, principal);

            // Add the claim
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();

                    var dbDevice = context.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == deviceName.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                    if (dbDevice == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = deviceName }));
                    }

                    var dbClaim = context.FirstOrDefault<DbDeviceClaim>(o => o.SourceKey == dbDevice.Key && o.ClaimType.ToLowerInvariant() == claim.Type.ToLowerInvariant());
                    if (dbClaim == null)
                    {
                        dbClaim = new DbDeviceClaim()
                        {
                            SourceKey = dbDevice.Key
                        };
                    }
                    dbClaim.ClaimType = claim.Type;
                    dbClaim.ClaimValue = claim.Value;

                    if (expiry.HasValue)
                    {
                        dbClaim.ClaimExpiry = DateTime.Now.Add(expiry.Value);
                    }

                    using (var tx = context.BeginTransaction())
                    {
                        dbClaim = context.InsertOrUpdate(dbClaim);
                        context.Update(dbDevice);
                        tx.Commit();
                    }

                    this.Changed?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dbDevice), principal));

                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DEV_CLAIM_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveClaim(string deviceName, string claimType, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(deviceName))
            {
                throw new ArgumentNullException(nameof(deviceName));
            }
            else if (String.IsNullOrEmpty(claimType))
            {
                throw new ArgumentNullException(nameof(claimType));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice, principal);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open();
                    var dbDevice = context.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == deviceName.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                    if (dbDevice == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = deviceName }));
                    }

                    dbDevice.UpdatedByKey = context.EstablishProvenance(principal);
                    dbDevice.UpdatedTime = DateTimeOffset.Now;

                    using (var tx = context.BeginTransaction())
                    {
                        context.DeleteAll<DbDeviceClaim>(o => o.SourceKey == dbDevice.Key && o.ClaimType.ToLowerInvariant() == claimType.ToLowerInvariant());
                        context.Update(dbDevice);
                        tx.Commit();
                    }
                    this.Changed?.Invoke(this, new IdentityEventArgs(new AdoDeviceIdentity(dbDevice), principal));

                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DEV_CLAIM_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(String deviceName)
        {
            if (String.IsNullOrEmpty(deviceName))
            {
                throw new ArgumentNullException(nameof(deviceName));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open();

                    var dbDeviceKey = context.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == deviceName.ToLowerInvariant() && o.ObsoletionTime == null).Select(o => o.Key).FirstOrDefault();
                    if (dbDeviceKey == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = deviceName }));
                    }
                    return context.Query<DbDeviceClaim>(o => o.SourceKey == dbDeviceKey && o.ClaimExpiry == null || o.ClaimExpiry > DateTime.Now).ToArray().Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue));
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DEV_CLAIM_GEN_ERR), e);
                }
            }
        }


    }
}