using System;
using SanteDB.OrmLite.Attributes;

namespace SanteDB.Persistence.Data.Model.Notifications
{
    /// <summary>
    /// Represents a notification template.
    /// </summary>
    [Table("notification_template_tbl")]
    public class DbNotificationTemplate : DbNonVersionedBaseData
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="DbNotificationTemplate"/> class.
        /// </summary>
        public DbNotificationTemplate()
        {

        }

        /// <summary>
        /// Gets or sets the key of the notification template.
        /// </summary>
        public override Guid Key { get; set; }
    }
}
