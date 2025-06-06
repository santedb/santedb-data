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
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security.Claims;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Concepts;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Roles;
using SanteDB.Persistence.Data.Model.Security;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Data.Security
{
    /// <summary>
    /// Represents a claims identity which is based on a DbSecurityUser
    /// </summary>
    internal class AdoUserIdentity : AdoIdentity
    {
        // The user which is stored in this identity
        private readonly DbSecurityUser m_securityUser;

        /// <summary>
        /// Creates a new user identity based on the user data
        /// </summary>
        /// <param name="userData">The user information from the authentication layer</param>
        /// <param name="authenticationMethod">The method used to authenticate (password, session, etc.)</param>
        internal AdoUserIdentity(DbSecurityUser userData, String authenticationMethod) : base(userData.UserName, authenticationMethod, true)
        {
            // Has the user been locked since the session was established?
            if (userData.Lockout > DateTimeOffset.Now)
            {
                throw new LockedIdentityAuthenticationException(userData.Lockout.Value);
            }
            else if (userData.ObsoletionTime.HasValue)
            {
                throw new InvalidIdentityAuthenticationException();
            }

            this.m_securityUser = userData;
            this.m_securityUser.Password = null;
            this.InitializeClaims();
        }

        /// <summary>
        /// The ADO user identity
        /// </summary>
        internal AdoUserIdentity(DbSecurityUser userData) : base(userData.UserName, null, false)
        {
            this.m_securityUser = userData;
            this.InitializeClaims();
        }

        /// <summary>
        /// Initialize the claims for this object
        /// </summary>
        private void InitializeClaims()
        {
            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.NameIdentifier, this.m_securityUser.Key.ToString()));
            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Name, this.m_securityUser.UserName));
            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBUserIdentifierClaim, this.m_securityUser.Key.ToString()));
            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SecurityId, this.m_securityUser.Key.ToString()));
            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Actor, this.m_securityUser.UserClass.ToString()));
            if (!String.IsNullOrEmpty(this.m_securityUser.Email) && this.m_securityUser.EmailConfirmed)
            {
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Email, this.m_securityUser.Email));
            }
            if (!String.IsNullOrEmpty(this.m_securityUser.PhoneNumber) && this.m_securityUser.PhoneNumberConfirmed)
            {
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Telephone, this.m_securityUser.PhoneNumber));
            }
        }

        /// <summary>
        /// Add role claims for the user authentication
        /// </summary>
        internal void AddRoleClaims(IEnumerable<String> roleNames)
        {
            this.AddClaims(roleNames.Select(o => new SanteDBClaim(SanteDBClaimTypes.DefaultRoleClaimType, o)));
        }

        /// <summary>
        /// Get the SID of this object
        /// </summary>
        internal override Guid Sid => this.m_securityUser.Key;

        /// <summary>
        /// Add relevant DCDR claims to the identity 
        /// </summary>
        internal void AddDcdrClaims(IEnumerable<DbUserClaim> contextClaims)
        {
            contextClaims.Where(o => o.ClaimType == SanteDBClaimTypes.LocalOnly).ForEach(c => this.AddClaim(new SanteDBClaim(c.ClaimType, c.ClaimValue)));
        }

        /// <summary>
        /// Add XSPA claims
        /// </summary>
        internal void AddXspaClaims(DataContext contextForReadingAdditionalData)
        {
            var cdrEntitySql = contextForReadingAdditionalData.CreateSqlStatementBuilder().SelectFrom(typeof(DbEntityVersion))
                    .InnerJoin<DbEntityVersion, DbUserEntity>(o => o.VersionKey, o => o.ParentKey)
                    .Where<DbUserEntity>(o => o.SecurityUserKey == this.Sid)
                    .And<DbEntityVersion>(o => o.IsHeadVersion)
                    .Statement;

            var cdrEntityId = contextForReadingAdditionalData.Query<DbEntityVersion>(cdrEntitySql).Select(o => o.Key).First();
            // NO CDR Entity
            if (cdrEntityId == Guid.Empty) return;

            this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.CdrEntityId, cdrEntityId.ToString()));

            if (this.FindFirst(SanteDBClaimTypes.XspaOrganizationIdClaim) == null)
            {
                var organizationId = contextForReadingAdditionalData.Query<DbEntityRelationship>(o => o.SourceKey == cdrEntityId && o.RelationshipTypeKey == EntityRelationshipTypeKeys.Employee && o.ObsoleteVersionSequenceId == null).Select(o => o.TargetKey);
                if (organizationId.Any())
                {
                    organizationId.ForEach(o => this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.XspaOrganizationIdClaim, o.ToString())));
                }
            }
            else
            {
                // Validate the claim
                var claimedOrgIds = this.FindAll(SanteDBClaimTypes.XspaOrganizationIdClaim).Select(o => Guid.Parse(o.Value)).ToArray();
                if (!contextForReadingAdditionalData.Any<DbEntityRelationship>(o => o.SourceKey == cdrEntityId && o.RelationshipTypeKey == EntityRelationshipTypeKeys.Employee && o.ObsoleteVersionSequenceId == null && claimedOrgIds.Contains(o.TargetKey)))
                {
                    throw new ClaimAssertionException(SanteDBClaimTypes.XspaOrganizationIdClaim, claimedOrgIds.First().ToString(), "*****");
                }
            }

            if (this.FindFirst(SanteDBClaimTypes.XspaFacilityClaim) == null)
            {
                var facilityId = contextForReadingAdditionalData.Query<DbEntityRelationship>(o => o.SourceKey == cdrEntityId && o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation && o.ObsoleteVersionSequenceId == null).Select(o => o.TargetKey);
                if (facilityId.Any())
                {
                    facilityId.ForEach(o => this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.XspaFacilityClaim, o.ToString())));
                }
            }
            else
            {
                // Validate the claim
                var claimedFacIds = this.FindAll(SanteDBClaimTypes.XspaFacilityClaim).Select(o => Guid.Parse(o.Value)).ToArray();
                var actualFacIds = contextForReadingAdditionalData.Query<DbEntityRelationship>(o => o.SourceKey == cdrEntityId && o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation && o.ObsoleteVersionSequenceId == null).Select(o => o.TargetKey).ToArray();
                if (actualFacIds.Any() && !claimedFacIds.All(f=>actualFacIds.Contains(f)))
                {
                    throw new ClaimAssertionException(SanteDBClaimTypes.XspaFacilityClaim, claimedFacIds.First().ToString(), String.Join(",", actualFacIds));
                }
            }

            var subjectNameSql = contextForReadingAdditionalData.CreateSqlStatementBuilder().SelectFrom(typeof(DbEntityNameComponent))
                .InnerJoin<DbEntityNameComponent, DbEntityName>(o => o.SourceKey, o => o.Key)
                .Where<DbEntityName>(o => o.SourceKey == cdrEntityId && o.ObsoleteVersionSequenceId == null && (o.UseConceptKey == NameUseKeys.OfficialRecord))
                .OrderBy<DbEntityNameComponent>(o => o.OrderSequence)
                .Statement;

            var nameValue = String.Join(" ", contextForReadingAdditionalData.Query<DbEntityNameComponent>(subjectNameSql).Select(o => o.Value));
            if (!String.IsNullOrEmpty(nameValue))
            {
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.XspaSubjectNameClaim, nameValue));
            }

            if (!this.FindAll(SanteDBClaimTypes.XspaUserRoleClaim).Any())
            {
                var subjectRoleSql = contextForReadingAdditionalData.CreateSqlStatementBuilder().SelectFrom(typeof(DbEntityRelationship), typeof(DbReferenceTerm), typeof(DbCodeSystem))
                    .InnerJoin<DbEntityRelationship, DbEntityVersion>(o => o.TargetKey, o => o.Key)
                    .InnerJoin<DbEntityVersion, DbProvider>(o => o.VersionKey, o => o.ParentKey)
                    .InnerJoin<DbProvider, DbConceptVersion>(o => o.SpecialtyKey, o => o.Key)
                    .Join<DbConceptVersion, DbConceptReferenceTerm>("LEFT", o => o.Key, o => o.SourceKey)
                    .InnerJoin<DbConceptReferenceTerm, DbReferenceTerm>(o => o.TargetKey, o => o.Key)
                    .InnerJoin<DbReferenceTerm, DbCodeSystem>(o => o.CodeSystemKey, o => o.Key)
                    .Where<DbEntityRelationship>(o => o.SourceKey == cdrEntityId && o.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity && o.ClassificationKey == RelationshipClassKeys.PlayedRoleLink && o.ObsoleteVersionSequenceId == null)
                    .And<DbEntityVersion>(o => o.ObsoletionTime == null)
                    .And<DbConceptReferenceTerm>(o => o.RelationshipTypeKey == ConceptRelationshipTypeKeys.SameAs && o.ObsoleteVersionSequenceId == null)
                    .Statement;

                foreach (var cd in contextForReadingAdditionalData.Query<CompositeResult<DbReferenceTerm, DbCodeSystem>>(subjectRoleSql))
                {
                    this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.XspaUserRoleClaim, $"{cd.Object1.Mnemonic}^{cd.Object2.Oid}"));
                }
            }

            // TODO: Retrieve NPI claim value
        }
    }
}