using SanteDB.OrmLite.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Model.Mail
{
    /// <summary>
    /// Receipt to record
    /// </summary>
    [Table("mail_msg_rcpt_to_tbl")]
    public class DbMailMessageRcptTo
    {
      
        /// <summary>
        /// Gets or sets the source key 
        /// </summary>
        [Column("mail_msg_id"), NotNull, ForeignKey(typeof(DbMailMessage), nameof(DbMailMessage.Key))]
        public Guid SourceKey { get; set; }

        /// <summary>
        /// Gets or sets the recipient key
        /// </summary>
        [Column("rcpt_id"), NotNull]
        public Guid RecipientKey { get; set; }

    }
}
