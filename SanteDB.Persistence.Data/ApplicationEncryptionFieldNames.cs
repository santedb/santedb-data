using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data
{
    /// <summary>
    /// Field names for ALE
    /// </summary>
    internal static class ApplicationEncryptionFieldNames
    {
        public const string NarrativeTitle = "narrative.title";
        public const string NarrativeText = "narrative.text";
        public const string TextObservationValue = "textObservation.value";
        public const string EntityIdentifier = "entity.identifier";
        public const string ActIdentifier = "act.identifier";
        public const string AddressComponent = "address.component";
        public const string NameComponent = "name.component";
        public const string TelecomValue = "telecom.value";
        public const string EntityNoteText = "entityNote.text";
        public const string ActNoteText = "actNote.text";
        public const string UserEmail = "user.email";
        public const string MailBody = "mail.body";
        public const string MailSubject = "mail.subject";
    }
}
