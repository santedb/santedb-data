/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Roles;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// Persistence service which handles the storage of Patient resources
    /// </summary>
    public class PatientPersistenceService : PersonDerivedPersistenceService<Patient, DbPatient>
    {
        // Fields which are restricted by default
        private readonly bool m_allowReligion;
        private readonly bool m_allowEthnicity;
        private readonly bool m_allowLivingArrangement;
        private readonly bool m_allowMaritalStatus;
        private readonly bool m_allowEducationLevel;

        // Fields which are permitted by default
        private readonly Guid[] m_forbiddenComponents;
        private readonly Dictionary<String, Guid> m_fobiddenComponentSettings = new Dictionary<string, Guid>()
        {
            { FieldRestrictionSettings.ForbidAddressCity, AddressComponentKeys.City },
            { FieldRestrictionSettings.ForbidAddressCounty, AddressComponentKeys.County },
            { FieldRestrictionSettings.ForbidAddressPostal, AddressComponentKeys.PostalCode },
            { FieldRestrictionSettings.ForbidAddressPrecinct, AddressComponentKeys.Precinct },
            { FieldRestrictionSettings.ForbidAddressState, AddressComponentKeys.State },
            { FieldRestrictionSettings.ForbidAddressStreet, AddressComponentKeys.StreetAddressLine },
            { FieldRestrictionSettings.ForbidNameGiven, NameComponentKeys.Given },
            { FieldRestrictionSettings.ForbidNameFamily, NameComponentKeys.Family },
            { FieldRestrictionSettings.ForbidNamePrefix, NameComponentKeys.Prefix },
            { FieldRestrictionSettings.ForbidNameSuffix, NameComponentKeys.Suffix }
        };

        /// <summary>
        /// DI Constructor
        /// </summary>
        public PatientPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
            _ = Boolean.TryParse(configurationManager.GetAppSetting(FieldRestrictionSettings.AllowReligion), out m_allowReligion);
            _ = Boolean.TryParse(configurationManager.GetAppSetting(FieldRestrictionSettings.AllowLivingArrangement), out m_allowLivingArrangement);
            _ = Boolean.TryParse(configurationManager.GetAppSetting(FieldRestrictionSettings.AllowEthnicity), out m_allowEthnicity);
            _ = Boolean.TryParse(configurationManager.GetAppSetting(FieldRestrictionSettings.AllowMaritalStatus), out m_allowMaritalStatus);
            _ = Boolean.TryParse(configurationManager.GetAppSetting(FieldRestrictionSettings.AllowEducationLevel), out m_allowEducationLevel);

            this.m_forbiddenComponents = this.m_fobiddenComponentSettings.Select(o => Boolean.TryParse(configurationManager.GetAppSetting(o.Key), out var forbid) && forbid ? o.Value : Guid.Empty)
                .Where(g => g != Guid.Empty).ToArray();
        }

        /// <inheritdoc />
        protected override Patient BeforePersisting(DataContext context, Patient data)
        {
            data.EducationLevelKey = this.EnsureExists(context, data.EducationLevel)?.Key ?? data.EducationLevelKey;
            if (data.EducationLevelKey.HasValue && !m_allowEducationLevel)
            {
                throw new FieldRestrictionException(nameof(Patient.EducationLevel));
            }

            data.EthnicGroupKey = this.EnsureExists(context, data.EthnicGroup)?.Key ?? data.EthnicGroupKey;
            if (data.EthnicGroupKey.HasValue && !m_allowEthnicity)
            {
                throw new FieldRestrictionException(nameof(Patient.EthnicGroup));
            }

            data.MaritalStatusKey = this.EnsureExists(context, data.MaritalStatus)?.Key ?? data.MaritalStatusKey;
            if (data.MaritalStatusKey.HasValue && !m_allowMaritalStatus)
            {
                throw new FieldRestrictionException(nameof(Patient.MaritalStatus));
            }

            data.LivingArrangementKey = this.EnsureExists(context, data.LivingArrangement)?.Key ?? data.LivingArrangementKey;
            if (data.LivingArrangementKey.HasValue && !m_allowLivingArrangement)
            {
                throw new FieldRestrictionException(nameof(Patient.LivingArrangement));
            }

            data.ReligiousAffiliationKey = this.EnsureExists(context, data.ReligiousAffiliation)?.Key ?? data.ReligiousAffiliationKey;
            if (data.ReligiousAffiliationKey.HasValue && !m_allowReligion)
            {
                throw new FieldRestrictionException(nameof(Patient.ReligiousAffiliation));
            }

            // Addresses and names containing forbidden fields?
            data.Addresses?.ForEach(a =>
            {
                if (a.Component?.Any(c => this.m_forbiddenComponents.Contains(c.ComponentTypeKey.GetValueOrDefault())) == true)
                {
                    throw new FieldRestrictionException(nameof(EntityAddress.Component));
                }
            });
            data.Names?.ForEach(a =>
            {
                if (a.Component?.Any(c => this.m_forbiddenComponents.Contains(c.ComponentTypeKey.GetValueOrDefault())) == true)
                {
                    throw new FieldRestrictionException(nameof(EntityName.Component));
                }
            });

            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override Patient DoConvertToInformationModelEx(DataContext context, DbEntityVersion dbModel, params object[] referenceObjects)
        {
            var modelData = base.DoConvertToInformationModelEx(context, dbModel, referenceObjects);

            var dbPatient = referenceObjects?.OfType<DbPatient>()?.FirstOrDefault();
            if (dbPatient == null)
            {
                this.m_tracer.TraceWarning("Using slow fetch of DbPatient for DbEntityVersion (consider using the appropriate persister class)");
                dbPatient = context.FirstOrDefault<DbPatient>(o => o.ParentKey == dbModel.VersionKey);
            }

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    var conceptPersister = typeof(Concept).GetRelatedPersistenceService() as IAdoPersistenceProvider<Concept>;
                    modelData.EducationLevel = conceptPersister.Get(context, dbPatient.EducationLevelKey.GetValueOrDefault());
                    modelData.SetLoaded(o => o.EducationLevel);
                    modelData.EthnicGroup = conceptPersister.Get(context, dbPatient.EthnicGroupKey.GetValueOrDefault());
                    modelData.SetLoaded(o => o.EthnicGroup);
                    modelData.LivingArrangement = conceptPersister.Get(context, dbPatient.LivingArrangementKey.GetValueOrDefault());
                    modelData.SetLoaded(o => o.LivingArrangement);
                    modelData.MaritalStatus = conceptPersister.Get(context, dbPatient.MaritalStatusKey.GetValueOrDefault());
                    modelData.SetLoaded(o => o.MaritalStatus);
                    modelData.ReligiousAffiliation = conceptPersister.Get(context, dbPatient.ReligiousAffiliationKey.GetValueOrDefault());
                    modelData.SetLoaded(o => o.ReligiousAffiliation);

                    break;
            }

            modelData.CopyObjectData(this.m_modelMapper.MapDomainInstance<DbPatient, Patient>(dbPatient), false, declaredOnly: true);

            return modelData;
        }
    }
}
