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
