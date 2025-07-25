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
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.EMMA;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.DataType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SanteDB.Persistence.Data.Services.Persistence
{
    /// <summary>
    /// Persistence service which handles versioned objects
    /// </summary>
    public abstract class VersionedDataPersistenceService<TModel, TDbModel, TDbKeyModel> : BaseEntityDataPersistenceService<TModel, TDbModel>, IExtendedDataQualityValidationProvider
        where TModel : BaseEntityData, IVersionedData, IHasState, new()
        where TDbModel : DbVersionedData, IDbHasStatus, new()
        where TDbKeyModel : DbIdentified, new()
    {

        // Validation configuration
        private readonly IDictionary<Type, AdoValidationPolicy> m_validationConfigurationCache = new ConcurrentDictionary<Type, AdoValidationPolicy>();

        // Default validation
        private readonly AdoValidationPolicy m_defaultValidationPolicy = new AdoValidationPolicy()
        {
            Authority = AdoValidationEnforcement.Off,
            Scope = AdoValidationEnforcement.Off,
            Uniqueness = AdoValidationEnforcement.Off,
            Format = AdoValidationEnforcement.Off,
            CheckDigit = AdoValidationEnforcement.Off
        };

        // Class key map
        private Guid[] m_classKeyMap;

        /// <inheritdoc/>
        Type[] IExtendedDataQualityValidationProvider.SupportedTypes => new Type[] { typeof(TModel) };

        /// <summary>
        /// Perform a copy of the existing version inforamtion to a new version
        /// </summary>
        /// <param name="context">The context on which the records should be inserted</param>
        /// <param name="newVersion">The new version key to be copied to</param>
        protected abstract void DoCopyVersionSubTableInternal(DataContext context, TDbModel newVersion);

        /// <inheritdoc/>
        protected override bool ValidateCacheItem(TModel cacheEntry, TDbModel dataModel) => cacheEntry.VersionSequence >= dataModel.VersionSequenceId && dataModel.IsHeadVersion;

        /// <summary>
        /// This method creates a new version from the old
        /// </summary>
        protected override TModel DoTouchModel(DataContext context, Guid key)
        {

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

#if DEBUG
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
#endif

                // We don't create new versions, instead we update the current data
                var existing = context.Query<TDbModel>(o => o.Key == key).OrderByDescending(o => o.VersionSequenceId).Skip(0).Take(2).ToArray();

                if (!existing.Any())
                {
                    throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { type = typeof(TModel).Name, id = key }));
                }

                // Are we creating a new verison? or no?
                if (!this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.FullVersioning))
                {
                    if (existing.Count() > 1) // We only keep recent and last
                    {
                        var lastVersionSequence = existing[0].VersionSequenceId;
                        this.DoDeleteAllModel(context, o => o.Key == key && o.VersionSequence < lastVersionSequence, DeleteMode.PermanentDelete);
                    }
                }
                else
                {
                    // We want to obsolete the non current version(s)
                    foreach (var itm in context.Query<TDbModel>(o => o.Key == key && o.ObsoletionTime == null).ToArray())
                    {
                        itm.ObsoletionTime = DateTimeOffset.Now;
                        itm.ObsoletedByKey = context.ContextId;
                        itm.ObsoletedByKeySpecified = itm.ObsoletionTimeSpecified = true;
                        context.Update(itm);
                    }
                }

                // next - we create a new version of dbmodel
                var oldVersion = existing.First();
                var newVersion = new TDbModel();
                newVersion.CopyObjectData(oldVersion);
                newVersion.ReplacesVersionKey = newVersion.VersionKey;
                newVersion.CreationTime = DateTimeOffset.Now;
                newVersion.CreatedByKey = context.ContextId;
                newVersion.ObsoletedByKey = null;
                newVersion.ObsoletionTime = null;
                newVersion.VersionSequenceId = null;
                newVersion.ObsoletedByKeySpecified = true;
                newVersion.VersionKey = Guid.NewGuid();
                newVersion.IsHeadVersion = true;

                if (oldVersion.IsHeadVersion)
                {
                    oldVersion.ObsoletionTime = DateTimeOffset.Now;
                    oldVersion.ObsoletedByKey = context.ContextId;
                    oldVersion.ObsoletedByKeySpecified = oldVersion.ObsoletionTimeSpecified = true;
                    oldVersion.IsHeadVersion = false;
                    context.Update(oldVersion);
                }

                context.Insert(newVersion);
                this.DoCopyVersionSubTableInternal(context, newVersion);

                return this.DoConvertToInformationModel(context, newVersion);
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceData(System.Diagnostics.Tracing.EventLevel.Verbose, $"PERFORMANCE: DoUpdateModel - {sw.ElapsedMilliseconds}ms", key, new StackTrace());
            }
#endif

        }
        /// <summary>
        /// Generate the specified constructor
        /// </summary>
        public VersionedDataPersistenceService(IConfigurationManager configurationManager,
            ILocalizationService localizationService,
            IAdhocCacheService adhocCacheService = null,
            IDataCachingService dataCachingService = null,
            IQueryPersistenceService queryPersistence = null
            ) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
            var ccatt = typeof(TModel).GetCustomAttributes<ClassConceptKeyAttribute>();
            if (typeof(IHasClassConcept).IsAssignableFrom(typeof(TModel)) && ccatt.Any())
            {
                this.m_classKeyMap = AppDomain.CurrentDomain.GetAllTypes().Where(o => typeof(TModel).IsAssignableFrom(o)).SelectMany(o => o.GetCustomAttributes<ClassConceptKeyAttribute>()).Select(o => Guid.Parse(o.ClassConcept)).ToArray();
            }
        }

        /// <summary>
        /// Verify identities on the specified entity
        /// </summary>
        protected virtual IEnumerable<DetectedIssue> VerifyEntity<TToVerify>(DataContext context, TToVerify objectToVerify)
            where TToVerify : TModel, IHasIdentifiers
        {
            if(objectToVerify?.ShouldDisablePersistenceValidation() == true ||  context.Data.TryGetValue(DataConstants.DisableObjectValidation, out var noValidation) && true.Equals(noValidation))
            {
                yield break;
            }

            // Validate unique values for IDs
            if (!this.m_validationConfigurationCache.TryGetValue(objectToVerify.GetType(), out var validation))
            {
                var applicableConfigurations = this.m_configuration.Validation.Where(t => t.Target == null || t.Target.Type.IsAssignableFrom(objectToVerify.GetType()));
                validation = this.m_defaultValidationPolicy;

                foreach (var itm in applicableConfigurations)
                {
                    validation.Authority = validation.Authority < itm.Authority ? itm.Authority : validation.Authority;
                    validation.CheckDigit = validation.CheckDigit < itm.CheckDigit ? itm.CheckDigit : validation.CheckDigit;
                    validation.Format = validation.Format < itm.Format ? itm.Format : validation.Format;
                    validation.Scope = validation.Scope < itm.Scope ? itm.Scope : validation.Scope;
                    validation.Uniqueness = validation.Uniqueness < itm.Uniqueness ? itm.Uniqueness : validation.Uniqueness;
                }
                this.m_validationConfigurationCache.Add(objectToVerify.GetType(), validation);
            }

            // No validation is enabled
            if (validation.Uniqueness == AdoValidationEnforcement.Off &&
                validation.Format == AdoValidationEnforcement.Off &&
                validation.CheckDigit == AdoValidationEnforcement.Off &&
                validation.Authority == AdoValidationEnforcement.Off &&
                validation.Scope == AdoValidationEnforcement.Off)
            {
                yield break;
            }
            
            foreach (var id in objectToVerify.Identifiers)
            {
                if (String.IsNullOrEmpty(id.Value)) continue; // skip empty

                // Get ID
                DbIdentityDomain dbAuth = null;
                var domainKey = id.IdentityDomain?.Key ?? id.IdentityDomainKey;

                if (id.IdentityDomainKey.HasValue)
                {
                    dbAuth = this.m_adhocCache?.Get<DbIdentityDomain>($"{DataConstants.AdhocAuthorityKey}{id.IdentityDomainKey}");
                    if (dbAuth == null)
                    {
                        dbAuth = context.FirstOrDefault<DbIdentityDomain>(o => o.Key == id.IdentityDomainKey);
                    }
                }
                else if (id.IdentityDomain == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.DEPENDENT_PROPERTY_NULL, "Authority"));
                }
                else if (domainKey.HasValue) // Attempt lookup in adhoc cache then by db
                {
                    dbAuth = this.m_adhocCache?.Get<DbIdentityDomain>($"{DataConstants.AdhocAuthorityKey}{domainKey}");
                    if (dbAuth == null)
                    {
                        dbAuth = context.FirstOrDefault<DbIdentityDomain>(o => o.Key == domainKey);
                    }
                }
                else
                {
                    dbAuth = this.m_adhocCache?.Get<DbIdentityDomain>($"{DataConstants.AdhocAuthorityKey}{id.IdentityDomain.DomainName}");
                    if (dbAuth == null)
                    {
                        dbAuth = context.FirstOrDefault<DbIdentityDomain>(o => o.DomainName == id.IdentityDomain.DomainName);
                        if (dbAuth != null)
                        {
                            id.IdentityDomain.Key = dbAuth.Key;
                        }
                    }
                }

                if (dbAuth == null)
                {
                    if (!(DataPersistenceControlContext.Current?.AutoInsertChildren ?? this.m_configuration.AutoInsertChildren) || id.IdentityDomain == null) // we're not inserting it and it doesn't exist - raise the alarm!
                    {
                        yield return new DetectedIssue(DetectedIssuePriorityType.Error, DataConstants.IdentifierDomainNotFound, $"Missing assigning authority with ID {String.Join(",", objectToVerify.Identifiers.Select(o => o.IdentityDomain?.Key ?? o.IdentityDomainKey))}", DetectedIssueKeys.SafetyConcernIssue);
                    }
                    continue;
                }
                else
                {
                    this.m_adhocCache?.Add($"{DataConstants.AdhocAuthorityKey}{domainKey}", dbAuth, new TimeSpan(0, 5, 0));
                    this.m_adhocCache?.Add($"{DataConstants.AdhocAuthorityKey}{dbAuth.DomainName}", dbAuth, new TimeSpan(0, 5, 0));
                }

                // Is there a check digit algorithm indicated?
                if (!String.IsNullOrEmpty(dbAuth.CheckDigitAlgorithm))
                {
                    if (String.IsNullOrEmpty(id.CheckDigit))
                    {
                        yield return new DetectedIssue(validation.CheckDigit.ToPriority(), DataConstants.IdentifierCheckDigitMissing, $"Identifier {id.Value} in domain {dbAuth.DomainName} is missing check digit", DetectedIssueKeys.InvalidDataIssue, objectToVerify.ToString());
                    }
                    var validatorType = Type.GetType(dbAuth.CheckDigitAlgorithm);
                    if (validatorType == null)
                    {
                        yield return new DetectedIssue(validation.CheckDigit.ToPriority(), DataConstants.IdentifierCheckProviderNotFound, $"Check digit provider {dbAuth.CheckDigitAlgorithm} is not found", DetectedIssueKeys.OtherIssue, objectToVerify.ToString());
                    }
                    var validator = Activator.CreateInstance(validatorType) as ICheckDigitAlgorithm;
                    if (validator?.ValidateCheckDigit(id.Value, id.CheckDigit) != true)
                    {
                        yield return new DetectedIssue(validation.CheckDigit.ToPriority(), DataConstants.IdentifierCheckDigitFailed, $"Check digit {id.CheckDigit} is not valid for identifier {id.Value}", DetectedIssueKeys.InvalidDataIssue, objectToVerify.ToString());
                    }
                }

                // Get this identifier records which is not owned by my record
                bool ownedByOthers, ownedByMe;

                if (objectToVerify is Entity ent)
                {
                    ownedByOthers = context.Query<DbEntityIdentifier>(
                        context.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbEntityIdentifier))
                        .Where<DbEntityIdentifier>(o => o.Value == id.Value && o.IdentityDomainKey == domainKey && o.ObsoleteVersionSequenceId == null && o.SourceKey != objectToVerify.Key)
                        .And("NOT EXISTS (SELECT 1 FROM ent_rel_tbl WHERE (src_ent_id = ? AND trg_ent_id = ent_id_tbl.ent_id OR trg_ent_id = ? AND src_ent_id = ent_id_tbl.ent_id) AND obslt_vrsn_seq_id IS NULL)", objectToVerify.Key, objectToVerify.Key) // Handles the case where the identifier is on a shared MASTER record for MDM
                        .Statement
                    ).Any();
                    ownedByMe = context.Query<DbEntityIdentifier>(
                        context.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbEntityIdentifier))
                        .Where<DbEntityIdentifier>(o => o.Value == id.Value && o.IdentityDomainKey == domainKey && o.ObsoleteVersionSequenceId == null)
                        .And("(ent_id = ? OR EXISTS (SELECT 1 FROM ent_rel_tbl WHERE (src_ent_id = ?  AND trg_ent_id = ent_id_tbl.ent_id) OR (trg_ent_id = ? AND src_ent_id = ent_id_tbl.ent_id) AND obslt_vrsn_seq_id IS NULL))", objectToVerify.Key, objectToVerify.Key, objectToVerify.Key)
                        .Statement
                    ).Any();
                }
                else
                {
                    ownedByOthers = context.Query<DbActIdentifier>(
                        context.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbActIdentifier))
                        .Where<DbActIdentifier>(o => o.Value == id.Value && o.IdentityDomainKey == domainKey && o.ObsoleteVersionSequenceId == null && o.SourceKey != objectToVerify.Key)
                        .And("NOT EXISTS (SELECT 1 FROM act_rel_tbl WHERE (src_act_id = ? AND trg_act_id = act_id_tbl.act_id OR trg_act_id = ? AND src_act_id = act_id_tbl.act_id) AND obslt_vrsn_seq_id IS NULL)", objectToVerify.Key, objectToVerify.Key)
                        .Statement
                    ).Any();
                    ownedByMe = context.Query<DbActIdentifier>(
                        context.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbActIdentifier))
                        .Where<DbActIdentifier>(o => o.Value == id.Value && o.IdentityDomainKey == domainKey && o.ObsoleteVersionSequenceId == null)
                        .And("(act_id = ? OR EXISTS (SELECT 1 FROM act_rel_tbl WHERE (src_act_id = ?  AND trg_act_id = act_id_tbl.act_id) OR (trg_act_id = ? AND src_act_id = act_id_tbl.act_id) AND obslt_vrsn_seq_id IS NULL))", objectToVerify.Key, objectToVerify.Key, objectToVerify.Key)
                        .Statement
                    ).Any();

                }

                // Verify scope
                IEnumerable<DbIdentityDomainScope> scopes = this.m_adhocCache?.Get<DbIdentityDomainScope[]>($"ado.aa.scp.{dbAuth.Key}");
                if (scopes == null)
                {
                    scopes = context.Query<DbIdentityDomainScope>(o => o.SourceKey == dbAuth.Key);
                    this.m_adhocCache?.Add($"{DataConstants.AdhocAuthorityScopeKey}{dbAuth.Key}", scopes.ToArray());
                }

                if (objectToVerify is IHasClassConcept classObject &&
                    scopes.Any() && !scopes.Any(s => s.ScopeConceptKey == classObject.ClassConceptKey) // This type of identifier is not allowed to be assigned to this type of object
                    && !ownedByOthers
                    && !ownedByMe) // Unless it was already associated to another type of object related to me
                {
                    yield return new DetectedIssue(validation.Scope.ToPriority(), DataConstants.IdentifierInvalidTargetScope, $"Identifier of type {dbAuth.DomainName} cannot be assigned to object of type {classObject.ClassConceptKey}", DetectedIssueKeys.BusinessRuleViolationIssue, objectToVerify.ToString());
                }

                // If the identity domain is unique, and we've been asked to raid identifier uq issues
                if (dbAuth.IsUnique &&
                    ownedByOthers)
                {
                    yield return new DetectedIssue(validation.Uniqueness.ToPriority(), DataConstants.IdentifierNotUnique, $"Identifier {id.Value} in domain {dbAuth.DomainName} violates unique constraint", DetectedIssueKeys.FormalConstraintIssue, objectToVerify.ToString());
                }

                var asgnAppKeys = this.m_adhocCache?.Get<Dictionary<Guid, IdentifierReliability>>($"{DataConstants.AdhocAuthorityAssignerKey}{dbAuth.Key}");
                if (asgnAppKeys == null)
                {
                    asgnAppKeys = context.Query<DbAssigningAuthority>(o => o.SourceKey == dbAuth.Key && o.ObsoletionTime == null).ToDictionary(o => o.AssigningApplicationKey, o => o.Reliability);
                    this.m_adhocCache?.Add($"{DataConstants.AdhocAuthorityAssignerKey}{dbAuth.Key}", asgnAppKeys);
                }
                if (asgnAppKeys.Any()) // Must have permission
                {
                    if (asgnAppKeys.TryGetValue(context.GetProvenance().ApplicationKey, out var reliability))
                    {
                        id.Reliability = reliability;
                    }
                    else if (!ownedByMe
                        && !ownedByOthers) // and has not already been assigned to me or anyone else (it is a new , unknown identifier)
                    {
                        id.Reliability = IdentifierReliability.Informative;
                        // Is the validation set to deny unauthorized assignment?
                        yield return new DetectedIssue(validation.Authority.ToPriority(), DataConstants.IdentifierNoAuthorityToAssign, $"Application does not have permission to assign {dbAuth.DomainName}", DetectedIssueKeys.SecurityIssue, objectToVerify.ToString());
                    }
                }

                if (!String.IsNullOrEmpty(dbAuth.ValidationRegex) && validation.Format != Configuration.AdoValidationEnforcement.Off) // must be valid
                {
                    var nonMatch = !new Regex(dbAuth.ValidationRegex).IsMatch(id.Value);
                    if (nonMatch)
                    {
                        yield return new DetectedIssue(validation.Format.ToPriority(), DataConstants.IdentifierPatternFormatFail, $"Identifier {id.Value} in domain {dbAuth.DomainName} failed format validation", DetectedIssueKeys.FormalConstraintIssue, objectToVerify.ToString());
                    }
                }

                if (validation.CheckDigit != Configuration.AdoValidationEnforcement.Off && !String.IsNullOrEmpty(dbAuth.CustomValidator))
                {
                    var type = Type.GetType(dbAuth.CustomValidator);
                    if (type == null)
                    {
                        yield return new DetectedIssue(validation.CheckDigit.ToPriority(), DataConstants.IdentifierValidatorProviderNotFound, $"Custom validator {dbAuth.CustomValidator} not found", DetectedIssueKeys.OtherIssue, objectToVerify.ToString());
                    }
                    var validator = Activator.CreateInstance(type) as IIdentifierValidator;
                    if (validator?.IsValid(id) != true)
                    {
                        yield return new DetectedIssue(validation.CheckDigit.ToPriority(), DataConstants.IdentifierValidatorFailed, $"Custom validator for {id.Value} in {dbAuth.DomainName} failed", DetectedIssueKeys.FormalConstraintIssue, objectToVerify.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Perform a GET internal
        /// </summary>
        protected override object DoGetInternal(DataContext context, Guid key, Guid? versionKey, bool allowCache = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            object retVal = default(TDbModel);

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif

                if (versionKey.GetValueOrDefault() == Guid.Empty) // fetching the current version
                {
                    var cacheKey = this.GetAdHocCacheKey(key);
                    if (allowCache && (this.m_configuration?.CachingPolicy?.Targets & Data.Configuration.AdoDataCachingPolicyTarget.DatabaseObjects) == Data.Configuration.AdoDataCachingPolicyTarget.DatabaseObjects)
                    {
                        retVal = this.m_adhocCache?.Get<TDbModel>(cacheKey);
                    }

                    // Cache miss
                    if (retVal == null)
                    {
                        retVal = context.Query<TDbModel>(o => o.Key == key && o.IsHeadVersion).FirstOrDefault();

                        if ((this.m_configuration?.CachingPolicy?.Targets & Data.Configuration.AdoDataCachingPolicyTarget.DatabaseObjects) == Data.Configuration.AdoDataCachingPolicyTarget.DatabaseObjects)
                        {
                            this.m_adhocCache.Add(cacheKey, retVal, this.m_configuration.CachingPolicy?.DataObjectExpiry);
                        }
                    }
                }
                else
                {
                    // Fetch the object
                    retVal = this.ExecuteQueryOrm(context, o => o.Key == key && o.VersionKey == versionKey).FirstOrDefault();
                }

#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Get {0}/v.{1} took {2} ms", key, versionKey, sw.ElapsedMilliseconds);
            }
#endif
            return retVal;
        }

        /// <summary>
        /// Perform an insert on the specified object
        /// </summary>
        protected override TDbModel DoInsertInternal(DataContext context, TDbModel dbModel)
        {
            if (dbModel == null)
            {
                throw new ArgumentNullException(nameof(dbModel), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif
                // First we want to insert the keyed object via TDbKeyTable
                if (dbModel.Key == Guid.Empty)
                {
                    dbModel.Key = Guid.NewGuid();
                }
                var dbKeyTable = context.Insert(new TDbKeyModel() { Key = dbModel.Key });

                // Next we want to insert the version key
                if (dbModel.VersionKey == Guid.Empty)
                {
                    dbModel.VersionKey = Guid.NewGuid();
                }
                dbModel.IsHeadVersion = true;
                dbModel.CreationTime = DateTimeOffset.Now;
                dbModel.CreatedByKey = context.ContextId;
                dbModel.VersionSequenceId = null;
                dbModel.ReplacesVersionKey = null;
                // Insert the version link data
                return context.Insert(dbModel);
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Insert {0} took {1}ms", dbModel, sw.ElapsedMilliseconds);
            }
#endif
        }

        /// <summary>
        /// Perform a versioned update
        /// </summary>
        protected override TDbModel DoUpdateInternal(DataContext context, TDbModel model)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (model == null)
            {
                throw new ArgumentNullException(nameof(model), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif

                // We don't create new versions, instead we update the current data
                var existing = context.Query<TDbModel>(o => o.Key == model.Key).OrderByDescending(o => o.VersionSequenceId).Skip(0).Take(2).ToArray();

                if (!existing.Any())
                {
                    throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { type = typeof(TModel).Name, id = model.Key }));
                }

                // Are we creating a new verison? or no?
                if (!this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.FullVersioning))
                {
                    if (existing.Count() > 1) // We only keep recent and last
                    {
                        var lastVersionSequence = existing[0].VersionSequenceId;
                        this.DoDeleteAllModel(context, o => o.Key == model.Key && o.VersionSequence < lastVersionSequence, DeleteMode.PermanentDelete);
                    }
                }
                else
                {
                    // We want to obsolete the non current version(s)
                    foreach (var itm in context.Query<TDbModel>(o => o.Key == model.Key && !o.ObsoletionTime.HasValue).ToArray())
                    {
                        itm.ObsoletionTime = DateTimeOffset.Now;
                        itm.ObsoletedByKey = context.ContextId;
                        itm.IsHeadVersion = false;
                        itm.ObsoletedByKeySpecified = itm.ObsoletionTimeSpecified = true;
                        context.Update(itm);
                    }
                }

                // next - we create a new version of dbmodel
                var oldVersion = existing.First();
                var newVersion = new TDbModel();
                newVersion.CopyObjectData(model, true);
                newVersion.ReplacesVersionKey = oldVersion.VersionKey;
                newVersion.CreationTime = DateTimeOffset.Now;
                newVersion.CreatedByKey = context.ContextId;
                newVersion.IsHeadVersion = true;
                newVersion.ObsoletedByKey = null;
                newVersion.ObsoletionTime = null;
                newVersion.VersionSequenceId = null;

                newVersion.ObsoletedByKeySpecified = model.ObsoletionTimeSpecified = true;
                newVersion.VersionKey = Guid.NewGuid();

                if (oldVersion.IsHeadVersion)
                {
                    oldVersion.ObsoletionTime = DateTimeOffset.Now;
                    oldVersion.ObsoletedByKey = context.ContextId;
                    oldVersion.IsHeadVersion = false;
                    oldVersion.ObsoletedByKeySpecified = oldVersion.ObsoletionTimeSpecified = true;
                    context.Update(oldVersion);
                }

                return context.Insert(newVersion); // Insert the core version
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Update of {0} took {1} ms", model, sw.ElapsedMilliseconds);
            }
#endif
        }

        /// <summary>
        /// Obsolete all objects
        /// </summary>
        protected override IEnumerable<Guid> DoDeleteAllInternal(DataContext context, Expression<Func<TModel, bool>> expression, DeleteMode deletionMode)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            if (expression == null)
            {
                throw new ArgumentException(nameof(expression), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_RANGE));
            }

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif
                // First - we determine if the query has an explicit status concept set
                if (typeof(IHasState).IsAssignableFrom(typeof(TModel)) && !expression.ToString().Contains(nameof(IHasState.StatusConceptKey)))
                {
                    var statusKeyProperty = Expression.MakeMemberAccess(expression.Parameters[0], typeof(TModel).GetProperty(nameof(IHasState.StatusConceptKey)));
                    statusKeyProperty = Expression.MakeMemberAccess(statusKeyProperty, statusKeyProperty.Type.GetProperty("Value"));
                    expression = Expression.Lambda<Func<TModel, bool>>(Expression.And(expression.Body, Expression.MakeBinary(ExpressionType.NotEqual, statusKeyProperty, Expression.Constant(StatusKeys.Obsolete))), expression.Parameters);
                }

                if (this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.FullVersioning))
                {
                    // Convert the query to a domain query so that the object persistence layer can turn the
                    // structured LINQ query into a SQL statement
                    var domainExpression = this.m_modelMapper.MapModelExpression<TModel, TDbModel, bool>(expression, false);
                    if (domainExpression == null)
                    {
                        this.m_tracer.TraceVerbose("WARNING: Using very slow DeleteAll() method - consider using only primary properties for delete all");
                        var columnKey = TableMapping.Get(typeof(TDbModel)).GetColumn(nameof(DbVersionedData.Key));
                        var keyQuery = context.GetQueryBuilder(this.m_modelMapper).CreateQuery(expression, columnKey).Statement;
                        var keys = context.Query<TDbModel>(keyQuery).Select(o => o.Key);
                        domainExpression = o => keys.Contains(o.Key);
                    }

                    // Add obsolete filter - only apply this to current versions
                    var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(domainExpression.Parameters[0], typeof(TDbModel).GetProperty(nameof(DbVersionedData.ObsoletionTime))), Expression.Constant(null));
                    domainExpression = Expression.Lambda<Func<TDbModel, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, domainExpression.Body), domainExpression.Parameters);

                    // determine our deletion mode
                    switch (deletionMode)
                    {
                        case DeleteMode.LogicalDelete:
                            context.UpdateAll(domainExpression, o => o.ObsoletionTime == DateTimeOffset.Now, o => o.ObsoletedByKey == context.ContextId);
                            foreach (var newVersion in context.Query<TDbModel>(o => o.ObsoletionTime != null && o.ObsoletedByKey == context.ContextId).Select(o => o.Key))
                            {
                                yield return newVersion;
                            }
                            yield break;
                        case DeleteMode.PermanentDelete:
                            foreach (var existing in context.Query<TDbModel>(domainExpression).ToArray())
                            {
                                this.DoDeleteReferencesInternal(context, existing.Key);
                                this.DoDeleteReferencesInternal(context, existing.VersionKey);
                                context.DeleteAll<TDbModel>(o => o.VersionKey == existing.VersionKey);

                                // Reverse the history
                                foreach (var ver in context.Query<TDbModel>(o => o.Key == existing.Key).OrderByDescending(o => o.VersionSequenceId).Select(o => o.VersionKey))
                                {
                                    this.DoDeleteReferencesInternal(context, ver);
                                    context.DeleteAll<TDbModel>(o => o.VersionKey == ver);

                                }

                                context.DeleteAll<TDbKeyModel>(o => o.Key == existing.Key);
                                this.m_dataCacheService.Remove(existing.Key);
                                yield return existing.Key;
                            }
                            break;
                    }
                }
                else
                {
                    base.DoDeleteAllInternal(context, expression, DeleteMode.PermanentDelete);
                }
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Obsolete all {0} took {1}ms", expression, sw.ElapsedMilliseconds);
            }
#endif
        }

        /// <summary>
        /// Perform an obsoletion of the object in the datamodel
        /// </summary>
        /// <param name="context">The context in which the obsoletion is occurring</param>
        /// <param name="key">The key of the object which is to be obsoleted</param>
        /// <param name="deletionMode">The mode of deletion</param>
        protected override TDbModel DoDeleteInternal(DataContext context, Guid key, DeleteMode deletionMode)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (key == Guid.Empty)
            {
                throw new ArgumentException(this.m_localizationService.GetString(ErrorMessageStrings.MISSING_ARGUMENT, nameof(key)));
            }

            // Perform the obsoletion
#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
            try
            {
#endif

                // How are we obsoleting this? - is it full versioning?
                if (this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.FullVersioning))
                {
                    // Get the current version
                    var existing = context.Query<TDbModel>(o => o.Key == key && o.IsHeadVersion).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { type = typeof(TModel).Name, id = key }));
                    }

                    TDbModel retVal = null;
                    switch (deletionMode)
                    {
                        case DeleteMode.LogicalDelete:

                            existing.ObsoletionTime = DateTimeOffset.Now;
                            existing.ObsoletedByKey = context.ContextId;
                            retVal = context.Update(existing);

                            break;
                        case DeleteMode.PermanentDelete:
                            this.DoDeleteReferencesInternal(context, existing.Key);
                            this.DoDeleteReferencesInternal(context, existing.VersionKey);
                            context.DeleteAll<TDbModel>(o => o.VersionKey == existing.VersionKey);
                            // Reverse the history
                            foreach (var ver in context.Query<TDbModel>(o => o.Key == existing.Key).OrderByDescending(o => o.VersionSequenceId).Select(o => o.VersionKey).ToList())
                            {
                                this.DoDeleteReferencesInternal(context, ver);
                                context.DeleteAll<TDbModel>(o => o.VersionKey == ver);
                            }

                            context.DeleteAll<TDbKeyModel>(o => o.Key == existing.Key);
                            existing.StatusConceptKey = StatusKeys.Purged;
                            existing.ObsoletionTime = DateTimeOffset.Now;
                            existing.ObsoletedByKey = context.ContextId;
                            existing.ReplacesVersionKey = existing.VersionKey;
                            existing.VersionKey = Guid.Empty;
                            return existing;

                        default:
                            throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_DELETE_MODE_SUPPORT, new { mode = deletionMode }));
                    }

                    // JF - This is not needed since the new method of delete doesn't
                    //      create a new version rather terminates the head
                    // Copy a new version of dependent tables
                    // this.DoCopyVersionSubTableInternal(context, retVal);

                    return retVal;

                }
                else
                {
                    return base.DoDeleteInternal(context, key, deletionMode);
                }
#if DEBUG
            }
            finally
            {
                sw.Stop();
                this.m_tracer.TraceVerbose("Obsoletion of {0} took {1} ms", key, sw.ElapsedMilliseconds);
            }
#endif
        }

        /// <inheritdoc/>
        public override SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(TDbModel));
            var headColumn = tableMap.GetColumn(nameof(DbVersionedData.IsHeadVersion));
            return new SqlStatementBuilder(this.Provider.StatementFactory, base.GetCurrentVersionFilter(tableAlias))
                .And($"{tableAlias ?? tableMap.TableName}.{headColumn.Name} = {this.Provider.StatementFactory.CreateSqlKeyword(SqlKeyword.True)}")
                .Statement;
        }

        /// <inheritdoc/>
        protected override Expression<Func<TModel, bool>> ApplyDefaultQueryFilters(Expression<Func<TModel, bool>> query)
        {

            // First - we determine if the query has an explicit status concept set
            // We don't need this anymore since we are using HEAD versions
            //if (typeof(IHasState).IsAssignableFrom(typeof(TModel)) && !query.ToString().Contains(nameof(IHasState.StatusConceptKey)))
            //{
            //    var statusKeyProperty = Expression.MakeMemberAccess(query.Parameters[0], typeof(TModel).GetProperty(nameof(IHasState.StatusConceptKey)));
            //    statusKeyProperty = Expression.MakeMemberAccess(statusKeyProperty, statusKeyProperty.Type.GetProperty("Value"));

            //    Expression obsoleteFilter = null;
            //    foreach (var itm in StatusKeys.InactiveStates)
            //    {
            //        var condition = Expression.MakeBinary(ExpressionType.NotEqual, statusKeyProperty, Expression.Constant(itm));
            //        if (obsoleteFilter == null)
            //        {
            //            obsoleteFilter = condition;
            //        }
            //        else
            //        {
            //            obsoleteFilter = Expression.AndAlso(condition, obsoleteFilter);
            //        }
            //    }
            //    query = Expression.Lambda<Func<TModel, bool>>(Expression.And(query.Body, obsoleteFilter), query.Parameters);
            //}

            // Is there any class concept key
            var queryString = query.ToString();
            if (m_classKeyMap != null && !queryString.Contains(nameof(IHasClassConcept.ClassConceptKey)))
            {
                var classKeyProperty = Expression.MakeMemberAccess(query.Parameters[0], typeof(TModel).GetProperty(nameof(IHasClassConcept.ClassConceptKey)));
                classKeyProperty = Expression.MakeMemberAccess(classKeyProperty, classKeyProperty.Type.GetProperty("Value"));

                Expression classFilter = null;
                foreach (var itm in this.m_classKeyMap)
                {
                    var condition = Expression.MakeBinary(ExpressionType.Equal, classKeyProperty, Expression.Constant(itm));
                    if (classFilter == null)
                    {
                        classFilter = condition;
                    }
                    else
                    {
                        classFilter = Expression.OrElse(condition, classFilter);
                    }
                }
                if (classFilter != null)
                {
                    query = Expression.Lambda<Func<TModel, bool>>(Expression.And(query.Body, classFilter), query.Parameters);
                }
            }

            // Ensure that we always apply HEAD filter if not applied

            if (!queryString.Contains(nameof(IVersionedData.IsHeadVersion)) && !queryString.Contains(nameof(IVersionedData.VersionKey)))
            {
                var headProperty = Expression.MakeMemberAccess(query.Parameters[0], typeof(TModel).GetProperty(nameof(IVersionedData.IsHeadVersion)));
                query = Expression.Lambda<Func<TModel, bool>>(Expression.And(query.Body, Expression.MakeBinary(ExpressionType.Equal, headProperty, Expression.Constant(true))), query.Parameters[0]);
            }

            if (!queryString.Contains(nameof(BaseEntityData.ObsoletionTime)) && !queryString.Contains(nameof(IVersionedData.VersionKey)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(query.Parameters[0], typeof(TModel).GetProperty(nameof(BaseEntityData.ObsoletionTime))), Expression.Constant(null));
                query = Expression.Lambda<Func<TModel, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, query.Body), query.Parameters);
            }
            return query;

        }

        /// <inheritdoc/>
        protected override IQueryResultSet<TModel> DoQueryModel(Expression<Func<TModel, bool>> query) => new MappedQueryResultSet<TModel>(this, nameof(IDbVersionedData.Key)).Where(query);

        /// <summary>
        /// Update associated entities
        /// </summary>
        /// <remarks>
        /// Updates the associated items of <typeparamref name="TModelAssociation"/> such that
        /// <paramref name="data"/>'s associations are updated to match the list
        /// provided in <paramref name="associations"/>
        /// </remarks>
        protected virtual IEnumerable<TModelAssociation> UpdateModelVersionedAssociations<TModelAssociation>(DataContext context, TModel data, IEnumerable<TModelAssociation> associations)
            where TModelAssociation : IdentifiedData, IVersionedAssociation, new()
        {

            if (data == null || data.Key.GetValueOrDefault() == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(IdentifiedData.Key), ErrorMessages.ARGUMENT_NULL);
            }

            context.PushData(DataConstants.NoTouchSourceContextKey, true);

            // We now want to fetch the perssitence serivce of this
            var persistenceService = typeof(TModelAssociation).GetRelatedPersistenceService() as IAdoPersistenceProvider<TModelAssociation>;
            if (persistenceService == null)
            {
                throw new DataPersistenceException(String.Format(ErrorMessages.RELATED_OBJECT_NOT_AVAILABLE, typeof(TModelAssociation), typeof(TModel)));
            }

            // Ensure either the relationship points to (key) (either source or target)
            associations = associations.Select(a =>
            {
                if (a is ITargetedAssociation target)
                {
                    if (target.TargetEntityKey != data.Key && !a.SourceEntityKey.HasValue) // The target is a target association
                    {
                        a.SourceEntityKey = data.Key;
                    }
                    else if (target.SourceEntityKey.HasValue && !target.TargetEntityKey.HasValue) // Reverse relationship
                    {
                        target.TargetEntityKey = data.Key;
                    }
                }
                else if (a.SourceEntityKey.GetValueOrDefault() == Guid.Empty)
                {
                    a.SourceEntityKey = data.Key;
                }

                if (!a.Key.HasValue && a.BatchOperation != BatchOperationType.Insert
                    && this.TryGetKeyResolver<TModelAssociation>(out var keyResolver))
                {
                    a.Key = persistenceService.Query(context, keyResolver.GetKeyExpression(a)).Select(o => o.Key).SingleOrDefault();
                }
                return a;
            }).ToArray();

            // Next we want to perform a relationship query to establish what is being loaded and what is being persisted
            // TODO: Determine if this line performs better than selecting the entire object (I suspect it would - but need to check)
            var contextKeys = context.Data.Where(o => o.Value is BatchOperationType bt && bt != BatchOperationType.Delete).Select(o => (Guid?)Guid.Parse(o.Key)).ToArray();
            var assocKeys = associations.Select(k => k.Key).ToArray();
            var existing = persistenceService.Query(context, o => o.SourceEntityKey == data.Key && o.ObsoleteVersionSequenceId == null || assocKeys.Contains(o.Key)).Select(o => o.Key).ToArray();
            var associationKeys = associations.Select(o => o.Key).ToArray();
            var toDelete = associations.Where(a => a.BatchOperation == BatchOperationType.Delete).Select(o => o.Key)
                .Union(existing.Where(e => !associationKeys.Contains(e)));

            toDelete = toDelete.Except(contextKeys).ToArray();
            // Anything to remove?
            if (toDelete.Any())
            {
                // Deletion mode or update mode for the associations as a batch
                var dbType = this.m_modelMapper.GetModelMapper(typeof(TModelAssociation)).TargetType;
                var whereClause = this.m_modelMapper.MapModelExpression<TModelAssociation, bool>(o => toDelete.Contains(o.Key), dbType);
                if (this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.AssociationVersioning))
                {
                    var updateClause = this.m_modelMapper.MapModelExpression<TModelAssociation, bool>(o => o.ObsoleteVersionSequenceId == data.VersionSequence, dbType);
                    context.UpdateAll(dbType, whereClause, updateClause);
                }
                else
                {
                    context.DeleteAll(dbType, whereClause);
                }
            }

            var addedRelationships = associations.Where(o => !o.IsEmpty() && o.BatchOperation != BatchOperationType.Delete && (!o.Key.HasValue || !existing.Contains(o.Key))).Select(a =>
           {
               a.EffectiveVersionSequenceId = data.VersionSequence;
               a = persistenceService.Insert(context, a);
               a.BatchOperation = Core.Model.DataTypes.BatchOperationType.Insert;
               return a;
           });
            var updatedRelationships = associations.Where(o => !o.IsEmpty() && o.BatchOperation != BatchOperationType.Delete && o.Key.HasValue && existing.Contains(o.Key)).Select(a =>
            {
                a = persistenceService.Update(context, a);
                a.BatchOperation = Core.Model.DataTypes.BatchOperationType.Update;
                return a;
            });

            var retVal = updatedRelationships.Union(addedRelationships).ToArray();
            context.PopData(DataConstants.NoTouchSourceContextKey, out _);
            return retVal;
        }

        /// <summary>
        /// Update the internal
        /// </summary>
        protected virtual IEnumerable<TAssociativeTable> UpdateInternalVersoinedAssociations<TAssociativeTable>(DataContext context, Guid sourceKey, long versionSequence, IEnumerable<TAssociativeTable> associations)
            where TAssociativeTable : IDbVersionedAssociation, new()
        {
            if (sourceKey == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(sourceKey), ErrorMessages.ARGUMENT_NULL);
            }

            // Ensure the source by locking the IEnumerable
            associations = associations.Select(a =>
            {
                if (a.SourceKey == Guid.Empty)
                {
                    a.SourceKey = sourceKey;
                }
                else if (a is IDbIdentified idi && idi.Key == Guid.Empty
                    && this.TryGetKeyResolver<TAssociativeTable>(out var resolver))
                {
                    idi.Key = context.Query<TAssociativeTable>(resolver.GetKeyExpression(a)).Select<Guid>(nameof(IDbIdentified.Key)).SingleOrDefault();
                }

                return a;
            }).ToArray();

            // Existing associations in the database
            var existing = context.Query<TAssociativeTable>(o => o.SourceKey == sourceKey && !o.ObsoleteVersionSequenceId.HasValue).ToArray();

            // Which ones are new?
            var contextKeys = context.Data.Where(o => o.Value is BatchOperationType bt && bt != BatchOperationType.Delete).Select(o => (TAssociativeTable)existing.OfType<IDbIdentified>().FirstOrDefault(i => i.Key == Guid.Parse(o.Key))).ToArray();
            var removeRelationships = existing.Where(e => !associations.Any(a => a.Equals(e)));
            removeRelationships = removeRelationships.Except(contextKeys);
            var addRelationships = associations.Where(a => !existing.Any(e => e.Equals(a)));

            // First, remove the old
            foreach (var itm in removeRelationships.Except(contextKeys))
            {
                this.m_tracer.TraceVerbose("Will remove {0} of {1}", typeof(TAssociativeTable).Name, itm);

                if (this.m_configuration.VersioningPolicy.HasFlag(Configuration.AdoVersioningPolicyFlags.AssociationVersioning))
                {
                    itm.ObsoleteVersionSequenceId = versionSequence;
                    context.Update(itm);
                }
                else
                {
                    context.Delete(itm);
                }
            }

            // Next, add the new
            foreach (var itm in addRelationships)
            {
                this.m_tracer.TraceVerbose("Will add {0} of {1}", typeof(TAssociativeTable).Name, itm);
                itm.EffectiveVersionSequenceId = versionSequence;
                context.Insert(itm);
            }

            return existing.Except(removeRelationships).Union(addRelationships).ToArray();
        }

        /// <inheritdoc/>
        public abstract IEnumerable<DetectedIssue> Validate(object objectToValidate);
        
    }
}