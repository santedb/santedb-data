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
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Core.Configuration;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Concepts;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Authentication;
using System.Security.Principal;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// An identity provider service that uses the ADO session table
    /// </summary>
    public class AdoSessionProvider : ISessionIdentityProviderService, ISessionProviderService, ILocalServiceProvider<ISessionIdentityProviderService>, ILocalServiceProvider<ISessionProviderService>
    {
        
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoSessionProvider));

        // Session configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;

        // Data signing service
        private readonly IDataSigningService m_dataSigningService;

        // Hashing service
        private readonly IPasswordHashingService m_passwordHashingService;

        // Security configuration
        private readonly SecurityConfigurationSection m_securityConfiguration;

        // PDP
        private readonly IPolicyDecisionService m_pdpService;

        // PIP
        private readonly IPolicyInformationService m_pipService;

        // Locale service
        private readonly ILocalizationService m_localizationService;

        // Ad-hoc cache used to store session information
        private readonly IAdhocCacheService m_adhocCacheService;

        // PEP
        private readonly IPolicyEnforcementService m_pepService;

        // TODO: Session caching in a memory cache

        private readonly String[] m_principalClaimsToSession =
        {
            SanteDBClaimTypes.XspaFacilityClaim,
            SanteDBClaimTypes.XspaOrganizationIdClaim,
            SanteDBClaimTypes.XspaOrganizationNameClaim,
            SanteDBClaimTypes.XspaPurposeOfUseClaim,
            SanteDBClaimTypes.XspaUserNpi,
            SanteDBClaimTypes.XspaUserRoleClaim,
            SanteDBClaimTypes.Language
        };

        /// <summary>
        /// Claims which are not to be stored or set in the session
        /// </summary>
        private readonly String[] m_nonSessionClaims = {
            SanteDBClaimTypes.Actor,
            SanteDBClaimTypes.AuthenticationInstant,
            SanteDBClaimTypes.AuthenticationMethod,
            SanteDBClaimTypes.AuthenticationType,
            SanteDBClaimTypes.DefaultNameClaimType,
            SanteDBClaimTypes.DefaultRoleClaimType,
            SanteDBClaimTypes.Email,
            SanteDBClaimTypes.Expiration,
            SanteDBClaimTypes.TemporarySession,
            SanteDBClaimTypes.Name,
            SanteDBClaimTypes.NameIdentifier,
            SanteDBClaimTypes.SanteDBApplicationIdentifierClaim,
            SanteDBClaimTypes.SanteDBDeviceIdentifierClaim,
            SanteDBClaimTypes.SanteDBOTAuthCode,
            SanteDBClaimTypes.SanteDBSessionIdClaim,
            SanteDBClaimTypes.SecurityId,
            SanteDBClaimTypes.Telephone
        };

        /// <summary>
        /// Creates a new ADO session identity provider with injected configuration manager
        /// </summary>
        public AdoSessionProvider(IConfigurationManager configuration,
            IDataSigningService dataSigning,
            ILocalizationService localizationService,
            IPasswordHashingService passwordHashingService,
            IPolicyDecisionService policyDecisionService,
            IPolicyInformationService policyInformationService,
            IPolicyEnforcementService policyEnforcementService,
            IAdhocCacheService adhocCache = null)
        {
            this.m_configuration = configuration.GetSection<AdoPersistenceConfigurationSection>();
            this.m_securityConfiguration = configuration.GetSection<SecurityConfigurationSection>();
            this.m_dataSigningService = dataSigning;
            this.m_passwordHashingService = passwordHashingService;
            this.m_pdpService = policyDecisionService;
            this.m_pepService = policyEnforcementService;
            this.m_pipService = policyInformationService;
            this.m_localizationService = localizationService;
            this.m_adhocCacheService = adhocCache;
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Databased Session Authentication Provider";

        /// <inheritdoc/>
        ISessionProviderService ILocalServiceProvider<ISessionProviderService>.LocalProvider => this;

        /// <inheritdoc/>
        ISessionIdentityProviderService ILocalServiceProvider<ISessionIdentityProviderService>.LocalProvider => this;

        /// <summary>
        /// Fired when a new session is established
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Established;

        /// <summary>
        /// Fired when a session is abandoned
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Abandoned;

        /// <summary>
        /// Fired when a session is abandoned
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Extended;

        /// <summary>
        /// Create cache key
        /// </summary>
        private string CreateCacheKey(Guid sessionKey)
        {
            return $"ado.ses.{this.m_passwordHashingService.ComputeHash(sessionKey.ToString())}";
        }

        /// <summary>
        /// Abandons the specified session
        /// </summary>
        public void Abandon(ISession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var sessionId = new Guid(session.Id);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        var dbSession = context.FirstOrDefault<DbSession>(o => o.Key == sessionId && o.NotAfter > DateTimeOffset.Now);
                        if (dbSession == null)
                        {
                            return; // Already abandoned
                        }
                        else
                        {
                            dbSession.NotAfter = dbSession.RefreshExpiration = DateTimeOffset.Now;
                            context.DeleteAll<DbSessionClaim>(o => o.SessionKey == dbSession.Key);
                        }

                        context.Update(dbSession);
                        tx.Commit();

                        this.m_adhocCacheService?.Remove(this.CreateCacheKey(sessionId));
                        this.m_adhocCacheService?.Remove($"{this.CreateCacheKey(sessionId)}.idt");

                        this.Abandoned?.Invoke(this, new SessionEstablishedEventArgs(AuthenticationContext.Current.Principal, session, true, false, null, null));
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Cannot abandon session {0} - {1}", BitConverter.ToString(session.Id), e);
                    this.Abandoned?.Invoke(this, new SessionEstablishedEventArgs(null, session, false, false, null, null));
                    throw new SecuritySessionException(SessionExceptionType.Other, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_ABANDON), e);
                }
            }
        }

        /// <summary>
        /// Authenticate (create a principal) based off a session
        /// </summary>
        public IPrincipal Authenticate(ISession session)
        {
            var identities = this.GetSessionIdentities(session, true, out AdoSecuritySession adoSession);
            return new AdoSessionPrincipal(adoSession, identities.OfType<IClaimsIdentity>());
        }

        /// <summary>
        /// Establish a new session with the specified principal
        /// </summary>
        public ISession Establish(IPrincipal principal, string remoteEp, bool isOverride, string purpose, string[] scope, string lang)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (!principal.Identity.IsAuthenticated)
            {
                throw new SecurityException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_NOT_AUTH_PRINCIPAL));
            }
            
            // Must be claims principal
            if (!(principal is IClaimsPrincipal claimsPrincipal))
            {
                throw new SecurityException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_NOT_CLAIMS_PRINCIPAL));
            }

            // Claims principals may set override and scope which trumps the user provided ones
            if (claimsPrincipal.HasClaim(o => o.Type == SanteDBClaimTypes.SanteDBScopeClaim))
            {
                scope = claimsPrincipal.FindAll(SanteDBClaimTypes.SanteDBScopeClaim).Select(o => o.Value).ToArray();
            }
            if (claimsPrincipal.HasClaim(o => o.Type == SanteDBClaimTypes.PurposeOfUse))
            {
                purpose = claimsPrincipal.FindFirst(SanteDBClaimTypes.PurposeOfUse).Value;
            }

            if (isOverride && (String.IsNullOrEmpty(purpose) || scope == null || scope.Length == 0))
            {
                var exception = new SecuritySessionException(SessionExceptionType.MissingRequiredClaim, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_OVERRIDE_WITH_INSUFFICIENT_DATA), null);
                exception.Data.Add(SecuritySessionException.DATA_CLAIM_TYPE_KEY, SanteDBClaimTypes.XspaPurposeOfUseClaim);
                exception.Data.Add(SecuritySessionException.DATA_CLAIM_VALUE_KEY, String.Join(",", PurposeOfUseKeys.AllKeys));
                throw exception;
            }

            // Ensure the prinicpal has permission to access scopes they have requested
            if (scope == null || scope.Length == 0)
            {
                scope = new string[] { "*" };
            }

            // Validate scopes are valid or can be overridden
            if (scope != null && !scope.Contains("*"))
            {
                foreach (var pol in scope.Select(o => this.m_pipService.GetPolicy(o)))
                {
                    var grant = this.m_pdpService.GetPolicyOutcome(principal, pol.Oid);
                    switch (grant)
                    {
                        case Core.Model.Security.PolicyGrantType.Deny:
                            throw new PolicyViolationException(principal, pol, grant);
                        case Core.Model.Security.PolicyGrantType.Elevate: // validate override
                            if (!pol.CanOverride)
                            {
                                throw new PolicyViolationException(principal, pol, Core.Model.Security.PolicyGrantType.Deny);
                            }
                            break;
                    }
                }
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    // When the system is configured only for one facility login then we want an XSPA facility ID to be set
                    // TODO: Determine whether this is the best place to perform this type of check
                    if (!isOverride &&
                        !this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AllowNonAssignedUsersToLogin, true) &&
                        !(principal.Identity is IDeviceIdentity || principal.Identity is IApplicationIdentity) &&
                        !this.m_pepService.SoftDemand(PermissionPolicyIdentifiers.LoginAnywhere, principal))
                    {
                        // What is the restricted facility identifier?
                        var assignedFacility = this.m_securityConfiguration.GetSecurityPolicy<Guid?>(SecurityPolicyIdentification.AssignedFacilityUuid, null);
                        var facilityClaims = claimsPrincipal.FindAll(SanteDBClaimTypes.XspaFacilityClaim);
                        // TODO: Allow login up hierarchy
                        if (!facilityClaims.Any() || !assignedFacility.HasValue && facilityClaims.Count() != 1) // The user has no facility claim nor do they have a "default" we can select
                        {
                            var exception = new SecuritySessionException(SessionExceptionType.MissingRequiredClaim, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_REQUIRE_FACILITY), null);
                            exception.Data.Add(SecuritySessionException.DATA_CLAIM_TYPE_KEY, SanteDBClaimTypes.XspaFacilityClaim);

                            // Fetch the allowed values for the facility selection
                            var sqlStatement = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbEntityName), typeof(DbEntityNameComponent))
                                .InnerJoin<DbEntityName, DbEntityNameComponent>(o => o.Key, o => o.SourceKey)
                                .Where<DbEntityName>(o => o.ObsoleteVersionSequenceId == null && o.UseConceptKey == NameUseKeys.OfficialRecord);

                            if(assignedFacility.HasValue)
                            {
                                sqlStatement = sqlStatement.And<DbEntityName>(o => o.SourceKey == assignedFacility.Value);
                            }
                            else if(facilityClaims.Any())
                            {
                                var userAssignedFacs = facilityClaims.Select(o => Guid.Parse(o.Value)).ToArray();
                                sqlStatement = sqlStatement.And<DbEntityName>(o => userAssignedFacs.Contains(o.SourceKey));
                            }
                            else
                            {
                                throw new SecuritySessionException(SessionExceptionType.NotEstablished, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_ASSIGNED_FACILITY_MISSING), null);
                            }

                            var dbn = context.Query<CompositeResult<DbEntityName, DbEntityNameComponent>>(sqlStatement.Statement);
                            exception.Data.Add(SecuritySessionException.DATA_CLAIM_VALUE_KEY, String.Join(",", dbn.ToArray().Select(o=>$"{o.Object1.SourceKey}={o.Object2.Value}")));
                            throw exception;
                        }
                        if (assignedFacility.HasValue &&
                            !facilityClaims.Any(c => c.Value == assignedFacility.ToString()))
                        {
                            throw new SecuritySessionException(SessionExceptionType.NotEstablished, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_ASSIGNED_FACILITY_MISMATCH, new { allowed = assignedFacility, assigned = String.Join(" or ", facilityClaims.Select(o => o.Value)) }), null);
                        }
                    }


                    using (var tx = context.BeginTransaction())
                    {
                        // Generate refresh token
                        IIdentity applicationId = claimsPrincipal.Identities.OfType<IApplicationIdentity>().FirstOrDefault(),
                            deviceId = claimsPrincipal.Identities.OfType<IDeviceIdentity>().FirstOrDefault(),
                            userId = claimsPrincipal.Identities.FirstOrDefault(o => !(o is IApplicationIdentity || o is IDeviceIdentity));

                        // Claims to be added to session
                        var claims = new List<IClaim>();

                        // Fetch the keys for the identities
                        Guid? applicationKey = null, deviceKey = null, userKey = null;

                        var audience = String.Empty;
                        // Application
                        switch (applicationId)
                        {
                            case AdoIdentity adoApplication:
                                applicationKey = adoApplication.Sid;
                                audience = context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == adoApplication.Sid && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? adoApplication.Name;
                                break;
                            case IClaimsIdentity claimApplication:
                                applicationKey = Guid.Parse(claimApplication.FindFirst(SanteDBClaimTypes.SecurityId)?.Value ?? claimApplication.FindFirst(SanteDBClaimTypes.SanteDBApplicationIdentifierClaim)?.Value);
                                audience = claimApplication.FindFirst(SanteDBClaimTypes.AudienceClaim)?.Value ?? context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == applicationKey && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? claimApplication.Name;
                                break;
                            case IIdentity idApplication:
                                applicationKey = context.FirstOrDefault<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == applicationId.Name.ToLowerInvariant())?.Key;
                                audience = context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == applicationKey && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? idApplication.Name;
                                break;
                            default:
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_NO_APPLICATION_ID));
                        }

                        // Device
                        switch (deviceId)
                        {
                            case AdoIdentity adoDevice:
                                deviceKey = adoDevice.Sid;
                                break;
                            case IClaimsIdentity claimDevice:
                                deviceKey = Guid.Parse(claimDevice.FindFirst(SanteDBClaimTypes.SecurityId)?.Value ?? claimDevice.FindFirst(SanteDBClaimTypes.SanteDBDeviceIdentifierClaim)?.Value);
                                break;
                            case IIdentity idDevice:
                                deviceKey = context.FirstOrDefault<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == deviceId.Name.ToLowerInvariant())?.Key;
                                break;
                        }

                        // User
                        switch (userId)
                        {
                            case AdoIdentity adoUser:
                                userKey = adoUser.Sid;
                                break;
                            case IClaimsIdentity claimUser:
                                userKey = Guid.Parse(claimUser.FindFirst(SanteDBClaimTypes.SecurityId)?.Value);
                                break;
                            case IIdentity idUser:
                                userKey = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userId.Name)?.Key;
                                break;
                        }


                        // Create refresh data
                        byte[] refreshToken = null;
                        var dbSession = new DbSession()
                        {
                            ApplicationKey = applicationKey.GetValueOrDefault(),
                            DeviceKey = deviceKey,
                            UserKey = userKey,
                            NotBefore = DateTimeOffset.Now,
                            RemoteEndpoint = remoteEp,
                            Audience = audience,
                            RefreshExpiration = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.RefreshLength, new TimeSpan(1, 0, 0)))
                        };

                        // Sessions with 
                        if (String.IsNullOrEmpty(purpose) && !isOverride && userKey != null)
                        {
                            refreshToken = new byte[32];
                            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(refreshToken);
                            dbSession.RefreshToken = this.m_passwordHashingService.ComputeHash(refreshToken).HexEncode().ToLower();
                        }

                        if (isOverride) // Overrides cannot be extended and can only be applied when the scopes allow it
                        {
                            dbSession.RefreshToken = null;
                            dbSession.RefreshExpiration = DateTimeOffset.Now;

                        }

                        // Is the original principal already a session principal from another service? If so we should use its expiration
                        if (principal is ITokenPrincipal itp)
                        {
                            dbSession.NotAfter = itp.ExpiresAt;
                        }
                        else
                        {
                            // Establish time limit
                            dbSession.NotAfter = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.SessionLength, new TimeSpan(1, 0, 0)));
                            // User is not really logging in, they are attempting to change their password only
                            if (scope?.Contains(PermissionPolicyIdentifiers.LoginPasswordOnly) == true &&
                                (purpose?.Equals(PurposeOfUseKeys.SecurityAdmin.ToString(), StringComparison.OrdinalIgnoreCase) == true ||
                                claimsPrincipal.FindFirst(SanteDBClaimTypes.PurposeOfUse)?.Value.Equals(PurposeOfUseKeys.SecurityAdmin.ToString(), StringComparison.OrdinalIgnoreCase) == true) ||
                                isOverride)
                            {
                                dbSession.NotAfter = DateTimeOffset.Now.AddSeconds(120); //TODO: Need to set this somewhere as a configuration setting. This means they have ~2 minutes to click on a password reset.
                            }
                        }


                        dbSession = context.Insert(dbSession);

                        // Default = *
                        var sessionScopes = new List<string>();
                        if (scope == null || scope.Contains("*"))
                        {
                            sessionScopes.AddRange(this.m_pdpService.GetEffectivePolicySet(principal).Where(o => o.Rule == Core.Model.Security.PolicyGrantType.Grant).Select(c => c.Policy.Oid));
                        }
                        if (scope?.Any(s => !s.Equals("*")) == true) // Demand additional scopes
                        {
                            sessionScopes.AddRange(scope.Where(s => !s.Equals("*")).Select(o => { this.m_pepService.Demand(o, principal); return o; }));
                        }

                        // Explicitly set scopes
                        sessionScopes.AddRange(scope.Where(s => !"*".Equals(s)));

                        // Add claims
                        claims.AddRange(sessionScopes.Distinct().Select(o => new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, o)));

                        // Override?
                        if (isOverride)
                        {
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBOverrideClaim, "true"));
                        }
                        // POU?
                        if (!String.IsNullOrEmpty(purpose))
                        {
                            // Convert POU from Guid (which it should be) to a MNEMONIC
                            if (Guid.TryParse(purpose, out var purposeId))
                            {
                                purpose = context.Query<DbConceptVersion>(o => o.Key == purposeId && o.ObsoletionTime == null).Select(o => o.Mnemonic).First();
                            }
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.PurposeOfUse, purpose));
                        }

                        // Specialized language for this session?
                        if (!String.IsNullOrEmpty(lang))
                        {
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.Language, lang));
                        }
                        else if(claimsPrincipal.TryGetClaimValue(SanteDBClaimTypes.Language, out lang))
                        {
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.Language, lang));
                        }

                        // Promote claims over
                        claims.AddRange(claimsPrincipal.Claims.Where(o => m_principalClaimsToSession.Contains(o.Type)));

                        // Local session/authentication
                        if(claimsPrincipal.HasClaim(o=>o.Type == SanteDBClaimTypes.LocalOnly && Boolean.TryParse(o.Value, out var b) && b))
                        {
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"));
                        }
                        
                        // Insert claims to database
                        var dbClaims = claims.Where(c => !this.m_nonSessionClaims.Contains(c.Type)).Select(o => new DbSessionClaim()
                        {
                            SessionKey = dbSession.Key,
                            ClaimType = o.Type,
                            ClaimValue = o.Value
                        });
                        context.InsertAll(dbClaims);

                        tx.Commit();

                        //var signedToken = dbSession.Key.ToByteArray().Concat(m_dataSigningService.SignData(dbSession.Key.ToByteArray())).ToArray();
                        //var signedRefresh = refreshToken.ToByteArray().Concat(m_dataSigningService.SignData(refreshToken.ToByteArray())).ToArray();
                        var session = new AdoSecuritySession(dbSession.Key.ToByteArray(), refreshToken, dbSession, dbClaims);

                        this.m_adhocCacheService?.Add(this.CreateCacheKey(session.Key), session, dbSession.RefreshExpiration.Subtract(DateTimeOffset.Now));

                        this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, session, true, isOverride, purpose, scope));

                        return session;
                    }
                }
                catch(SecuritySessionException)
                {
                    throw;
                }
                catch (NullReferenceException e)
                {
                    this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, null, false, isOverride, purpose, scope));
                    throw new SecuritySessionException(SessionExceptionType.NotEstablished, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_MISSING_IDENTITY_DATA), e);
                }
                catch (Exception e)
                {
                    this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, null, false, isOverride, purpose, scope));
                    throw new SecuritySessionException(SessionExceptionType.NotEstablished, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), e);
                }
            }
        }

        /// <summary>
        /// Extend the provided session with the refresh token provided
        /// </summary>
        public ISession Extend(byte[] refreshToken)
        {
            if (refreshToken == null)
            {
                throw new ArgumentNullException(nameof(refreshToken), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        var refreshHash = this.m_passwordHashingService.ComputeHash(refreshToken).HexEncode().ToLowerInvariant();
                        var dbSession = context.SingleOrDefault<DbSession>(o => o.RefreshToken == refreshHash);

                        if (dbSession == null)
                        {
                            throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_TOKEN_INVALID));
                        }
                        else if (dbSession.NotAfter < DateTimeOffset.Now)
                        {
                            throw new SecuritySessionException(SessionExceptionType.Expired, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_REFRESH_EXPIRE), null);
                        }


                        // Provided Application
                        var providedAudience = String.Empty;
                        switch ((AuthenticationContext.Current.Principal as IClaimsPrincipal)?.Identities.OfType<IApplicationIdentity>().FirstOrDefault())
                        {
                            case AdoIdentity adoApplication:
                                providedAudience = context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == adoApplication.Sid && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? adoApplication.Name;
                                break;
                            case IClaimsIdentity claimApplication:
                                var applicationKey = context.FirstOrDefault<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == claimApplication.Name.ToLowerInvariant())?.Key;
                                providedAudience = claimApplication.FindFirst(SanteDBClaimTypes.AudienceClaim)?.Value ?? context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == applicationKey && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? claimApplication.Name;
                                break;
                            case IIdentity idApplication:
                                applicationKey = context.FirstOrDefault<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == idApplication.Name.ToLowerInvariant())?.Key;
                                providedAudience = context.FirstOrDefault<DbApplicationClaim>(o => o.SourceKey == applicationKey && o.ClaimType == SanteDBClaimTypes.AudienceClaim && (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now))?.ClaimValue ?? idApplication.Name;
                                break;
                            default:
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_NO_APPLICATION_ID));
                        }

                        // Validate that the audience has not changed!
                        if (providedAudience != dbSession.Audience)
                        {
                            throw new SecuritySessionException(SessionExceptionType.Other, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_AUDIENCE_CHANGED), null);
                        }

                        var dbClaims = context.Query<DbSessionClaim>(o => o.SessionKey == dbSession.Key).ToList();

                        // Validate - Override sessions cannot be extended
                        if (dbClaims.Any(c => (c.ClaimType == SanteDBClaimTypes.TemporarySession || c.ClaimType == SanteDBClaimTypes.SanteDBOverrideClaim) && c.ClaimValue == "true" || c.ClaimType == SanteDBClaimTypes.PurposeOfUse && c.ClaimValue == PurposeOfUseKeys.SecurityAdmin.ToString()))
                        {
                            throw new SecurityException(this.m_localizationService.GetString(ErrorMessageStrings.ELEVATED_SESSION_NO_EXTENSION));
                        }

                        // Generate a new session for this
                        var newRefreshToken = new byte[32];
                        System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(newRefreshToken);
                        dbSession.RefreshToken = this.m_passwordHashingService.ComputeHash(newRefreshToken).HexEncode().ToLowerInvariant();
                        dbSession.RefreshExpiration = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.RefreshLength, new TimeSpan(1, 0, 0)));
                        dbSession.NotAfter = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.SessionLength, new TimeSpan(1, 0, 0)));
                        dbSession.NotBefore = DateTimeOffset.Now;
                        dbSession = context.Update(dbSession);

                        tx.Commit();

                        //var signedToken = dbSession.Key.ToByteArray().Concat(m_dataSigningService.SignData(dbSession.Key.ToByteArray())).ToArray();
                        //var signedRefresh = refreshTokenId.ToByteArray().Concat(m_dataSigningService.SignData(refreshTokenId.ToByteArray())).ToArray();
                        var session = new AdoSecuritySession(dbSession.Key.ToByteArray(), newRefreshToken, dbSession, dbClaims);
                        this.Extended?.Invoke(this, new SessionEstablishedEventArgs(null, session, true, false,
                            session.FindFirst(SanteDBClaimTypes.PurposeOfUse)?.Value,
                            session.Find(SanteDBClaimTypes.SanteDBScopeClaim)?.Select(o => o.Value).ToArray()));

                        this.m_adhocCacheService?.Add(this.CreateCacheKey(session.Key), session, dbSession.RefreshExpiration.Subtract(DateTimeOffset.Now));
                        return session;
                    }
                }
                catch (SecuritySessionException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error extending session: {0}", e);
                    throw new SecuritySessionException(SessionExceptionType.Other, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">User guid is empty or invalid</exception>
        /// <exception cref="DataPersistenceException">An error occurred with the data persistence layer.</exception>
        public ISession[] GetUserSessions(Guid user)
        {
            if (user == Guid.Empty)
            {
                throw new ArgumentException(m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_RANGE), nameof(user));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var now = DateTimeOffset.Now;

                    var sessions = context.Query<DbSession>(
                        s => s.UserKey == user && s.NotAfter >= now
                        )?.ToList();

                    return sessions
                        .Select(s => new AdoSecuritySession(s.Key.ToByteArray(), null, s, context.Query<DbSessionClaim>(sc => sc.SessionKey == s.Key))).ToArray();
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    this.m_tracer.TraceError("Error getting session data {0}", ex);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), ex);
                }
            }
        }

        /// <summary>
        /// Get the specified session
        /// </summary>
        public ISession Get(byte[] sessionId, bool allowExpired = false)
        {
            if (sessionId == null)
            {
                throw new ArgumentNullException(nameof(sessionId), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var sessionguid = new Guid(sessionId);

            AdoSecuritySession sessionInfo = null;
            if (this.m_adhocCacheService.TryGet<AdoSecuritySession>(this.CreateCacheKey(sessionguid), out sessionInfo))
            {
                if (!allowExpired && sessionInfo.NotAfter < DateTimeOffset.Now)
                {
                    throw new SecuritySessionException(SessionExceptionType.Expired, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_EXPIRE), null);
                }
                return new AdoSecuritySession(sessionInfo);
            }
            else
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    try
                    {
                        context.Open(initializeExtensions: false);

                        var dbSession = context.SingleOrDefault<DbSession>(o => o.Key == sessionguid);
                        if (dbSession == null)
                        {
                            return null;
                        }
                        else if (!allowExpired && dbSession.NotAfter < DateTimeOffset.Now)
                        {
                            throw new SecuritySessionException(SessionExceptionType.Expired, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_EXPIRE), null);
                        }
                        else
                        {
                            return new AdoSecuritySession(sessionId, null, dbSession, context.Query<DbSessionClaim>(o => o.SessionKey == dbSession.Key));
                        }
                    }
                    catch (SecuritySessionException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error getting session data {0}", e);
                        throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), e);
                    }
                }
            }
        }

        /// <summary>
        /// Get all identities which are part of the session
        /// </summary>
        public IIdentity[] GetIdentities(ISession session) => this.GetSessionIdentities(session, false, out AdoSecuritySession _);

        /// <summary>
        /// Get all identiites for the valid session
        /// </summary>
        private IIdentity[] GetSessionIdentities(ISession session, bool authenticated, out AdoSecuritySession adoSession)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var sessionId = new Guid(session.Id);

            adoSession = this.m_adhocCacheService?.Get<AdoSecuritySession>(this.CreateCacheKey(sessionId));
            var identities = this.m_adhocCacheService?.Get<IIdentity[]>($"{this.CreateCacheKey(sessionId)}.idt");
            if (adoSession != null && identities != null)
            {
                adoSession = new AdoSecuritySession(adoSession);
                return identities.OfType<IIdentity>().ToArray();
            }
            else
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    try
                    {
                        context.Open(initializeExtensions: false);
                        var sql = context.CreateSqlStatementBuilder()
                            .SelectFrom(typeof(DbSession), typeof(DbSecurityApplication), typeof(DbSecurityUser), typeof(DbSecurityDevice))
                            .InnerJoin<DbSession, DbSecurityApplication>(o => o.ApplicationKey, o => o.Key)
                            .Join<DbSession, DbSecurityUser>("LEFT", o => o.UserKey, o => o.Key)
                            .Join<DbSession, DbSecurityDevice>("LEFT", o => o.DeviceKey, o => o.Key)
                            .Where<DbSession>(o => o.Key == sessionId)
                            .Statement;
                        var dbSession = context.FirstOrDefault<CompositeResult<DbSession, DbSecurityApplication, DbSecurityUser, DbSecurityDevice>>(sql);

                        if (dbSession == null)
                        {
                            throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_TOKEN_INVALID));
                        }
                        else if (authenticated && dbSession.Object1.NotAfter < DateTimeOffset.Now)
                        {
                            throw new SecuritySessionException(SessionExceptionType.Expired, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_EXPIRE), null);
                        }

                        adoSession = new AdoSecuritySession(session.Id, null, dbSession.Object1, context.Query<DbSessionClaim>(o => o.SessionKey == dbSession.Object1.Key));

                        // Precendence of identiites in the principal : User , App, Device
                        identities = new IClaimsIdentity[3];
                        if (dbSession.Object3.Key != Guid.Empty)
                        {
                            if (authenticated)
                            {
                                var uid = new AdoUserIdentity(dbSession.Object3, "SESSION");
                                uid.AddClaims(adoSession.Claims.Where(o => m_principalClaimsToSession.Contains(o.Type)));
                                uid.AddXspaClaims(context);
                                identities[0] = uid;
                            }
                            else
                            {
                                identities[0] = new AdoUserIdentity(dbSession.Object3);
                            }
                        }
                        if (dbSession.Object2.Key != Guid.Empty)
                        {
                            if (authenticated)
                            {
                                identities[1] = new AdoApplicationIdentity(dbSession.Object2, "SESSION");
                            }
                            else
                            {
                                identities[1] = new AdoApplicationIdentity(dbSession.Object2);
                            }
                        }
                        if (dbSession.Object4.Key != Guid.Empty)
                        {
                            if (authenticated)
                            {
                                identities[2] = new AdoDeviceIdentity(dbSession.Object4, "SESSION");
                            }
                            else
                            {
                                identities[2] = new AdoDeviceIdentity(dbSession.Object4);
                            }
                        }

                        this.m_adhocCacheService?.Add($"{this.CreateCacheKey(sessionId)}.idt", identities);

                        return identities.OfType<IIdentity>().ToArray();
                    }
                    catch (InvalidIdentityAuthenticationException)
                    {
                        throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_IDENTITY_INVALID));
                    }
                    catch (LockedIdentityAuthenticationException)
                    {
                        throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_IDENTITY_LOCKED));
                    }
                    catch (SecuritySessionException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error authenticating based on session data - {0}", e);
                        throw new SecuritySessionException(SessionExceptionType.NotEstablished, this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), e);
                    }
                }
            }
        }

        /// <summary>
        /// Get all active sessions 
        /// </summary>
        public ISession[] GetActiveSessions()
        {
            this.m_pepService.Demand(PermissionPolicyIdentifiers.UnrestrictedAdministration); // Must be administrator to have this event return

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var sessions = context.Query<DbSession>(
                        s => s.NotAfter >= DateTimeOffset.Now
                        )?.ToList();

                    return sessions
                        .Select(s => new AdoSecuritySession(s.Key.ToByteArray(), null, s, context.Query<DbSessionClaim>(sc => sc.SessionKey == s.Key))).ToArray();
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    this.m_tracer.TraceError("Error getting session data {0}", ex);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SESSION_GEN_ERR), ex);
                }
            }
        }
    }
}