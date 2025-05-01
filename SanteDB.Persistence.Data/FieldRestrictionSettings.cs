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
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data
{
    /// <summary>
    /// Field restrictions that the jurisdiction may place on the persistence layer
    /// </summary>
    internal static class FieldRestrictionSettings
    {

        public const string ForbidNameFamily = "forbid.patient.name.family";
        public const string ForbidNameGiven = "forbid.patient.name.given";
        public const string ForbidNamePrefix = "forbid.patient.name.prefix";
        public const string ForbidNameSuffix = "forbid.patient.name.suffix";
     
        public const string ForbidAddressState = "forbid.patient.address.state";
        public const string ForbidAddressCounty = "forbid.patient.address.county";
        public const string ForbidAddressCity = "forbid.patient.address.city";
        public const string ForbidAddressPrecinct = "forbid.patient.address.precinct";
        public const string ForbidAddressStreet = "forbid.patient.address.street";
        public const string ForbidAddressPostal = "forbid.patient.address.postalcode";

        public const string AllowReligion = "allow.patient.religion";
        public const string AllowEthnicity = "allow.patient.ethnicity";
        public const string AllowLivingArrangement = "allow.patient.livingArrangement";
        public const string AllowMaritalStatus = "allow.patient.martialStatus";
        public const string AllowEducationLevel = "allow.patient.educationLevel";
    }
}
