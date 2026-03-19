/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using DocumentFormat.OpenXml.Spreadsheet;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Mail;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace SanteDB.Persistence.Data.Test.SQLite.Persistence.Mail
{
    /// <summary>
    /// Persistence of mailboxes and sending of mail test
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class LocalMailManagementPersistenceTest : DataPersistenceTest
    {

        /// <summary>
        /// Can send a mail message
        /// </summary>
        [Test]
        public void TestCanSendMailMessage()
        {
            // Create the TO user
            var securityService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityUser>>();
            var identityService = ApplicationServiceContext.Current.GetService<IIdentityProviderService>();
            var mailService = ApplicationServiceContext.Current.GetService<IMailMessageService>();
            Assert.IsNotNull(securityService);
            Assert.IsNotNull(mailService);

            // Construct a mail message and send as system
            using (AuthenticationContext.EnterSystemContext())
            {


                var toUser = securityService.Insert(new Core.Model.Security.SecurityUser()
                {
                    UserName = "TEST_MAIL_TO1",
                    Email = "test@test.com",
                    Password = "@Foo123!!"
                });
                Assert.IsNotNull(toUser);

                Thread.Sleep(250);

                var mailMessage = new MailMessage("SYSTEM", "TEST_MAIL_TO1", "This is a test", "This is a test message / alert sent within SanteDB", MailMessageFlags.HighPriority);
                var afterSent = mailService.Send(mailMessage);
                Assert.IsTrue(mailMessage.RcptToXml.Contains(toUser.Key.Value), "Message does not contain RCPT TO");
                Assert.AreEqual(mailMessage.Subject, afterSent.Subject);
                Assert.AreEqual(mailMessage.Body, afterSent.Body);

                mailMessage = new MailMessage("SYSTEM", "TEST_MAIL_TO1", "This is another test", "This is another test message / alert sent within SanteDB", MailMessageFlags.LowPriority);
                afterSent = mailService.Send(mailMessage);

                // Should have created an inbox
                var inbox = mailService.GetMailboxes(toUser.Key).FirstOrDefault(o => o.Name == Mailbox.INBOX_NAME);
                Assert.IsNotNull(inbox);
                Assert.AreEqual(toUser.Key, inbox.OwnerKey);
                Assert.IsInstanceOf<SecurityUser>(inbox.LoadProperty(o => o.Owner));
                Assert.AreEqual("TEST_MAIL_TO1", (inbox.LoadProperty(o => o.Owner) as SecurityUser).UserName);

            }

            // Now authenticate as our user and check the mail
            using (AuthenticationContext.EnterContext(identityService.Authenticate("TEST_MAIL_TO1", "@Foo123!!")))
            {
                var inbox = mailService.GetMailboxes().Where(o => o.Name == Mailbox.INBOX_NAME).FirstOrDefault();
                Assert.IsNotNull(inbox);
                Assert.GreaterOrEqual(inbox.LoadProperty(o => o.Messages).Count, 2);
                var messages = mailService.GetMessages(inbox.Key.Value);
                Assert.GreaterOrEqual(messages.Count(), 2);

                // Now we want to test the sorting and search of the mailbox
                Assert.AreEqual(1, messages.Where(s => s.TargetEntity.Subject == "This is a test").Count());
                Assert.AreEqual(0, messages.Where(s => s.TargetEntity.Body == "This is a test").Count());
                Assert.AreEqual("This is another test", messages.Where(s => s.TargetEntity.Flags == MailMessageFlags.LowPriority).OrderByDescending(o=>o.DeliveredTime).First().LoadProperty(o => o.TargetEntity).Subject);

                Assert.AreEqual(MailStatusFlags.Unread, messages.OrderByDescending(o => o.MailStatusFlag).First().MailStatusFlag);

            }

        }

        /// <summary>
        /// Tests that the appropriate permissions are applied when creating a mailbox 
        /// </summary>
        [Test]
        public void TestCanCreateMailbox()
        {
            // Create the TO user
            var securityService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityUser>>();
            var identityService = ApplicationServiceContext.Current.GetService<IIdentityProviderService>();
            var roleService = ApplicationServiceContext.Current.GetService<IRoleProviderService>();
            var mailService = ApplicationServiceContext.Current.GetService<IMailMessageService>();
            Assert.IsNotNull(securityService);
            Assert.IsNotNull(mailService);

            // Construct a mail message and send as system
            using (AuthenticationContext.EnterSystemContext())
            {

                // Create a user
                var toUser = securityService.Insert(new Core.Model.Security.SecurityUser()
                {
                    UserName = "TEST_MAIL_TO2",
                    Email = "test2@test.com",
                    Password = "@Foo123!!"
                });
                roleService.AddUsersToRoles(new string[] { "TEST_MAIL_TO2" }, new string[] { "USERS", "CLINICAL_STAFF" }, AuthenticationContext.SystemPrincipal);
                Assert.IsNotNull(toUser);

                // SYSTEM can read mailbox for user
                mailService.GetMailboxes(toUser.Key);
                Assert.IsNotNull(mailService.GetMailboxByName(Mailbox.INBOX_NAME));

            }

            // We want a delay so the welcome message is before our messages in test
            Thread.Sleep(250);
            // As user
            using (AuthenticationContext.EnterContext(identityService.Authenticate("TEST_MAIL_TO2", "@Foo123!!")))
            {
                // User cannot create mailbox for SYSTEM
                Assert.Throws<PolicyViolationException>(() => mailService.CreateMailbox(Mailbox.INBOX_NAME, Guid.Parse(AuthenticationContext.SystemUserSid)));
                // User cannot read mailboxes for SYSTEM
                Assert.Throws<PolicyViolationException>(() => mailService.GetMailboxes(Guid.Parse(AuthenticationContext.SystemUserSid)));

                // User can create their own mailbox
                var mailbox = mailService.CreateMailbox("FOO!");
                Assert.AreEqual(4, mailService.GetMailboxes().Count());

                // User can send mail to SYSTEM
                var mail = mailService.Send(new MailMessage("TEST_MAIL_TO2", "SYSTEM;TEST_MAIL_TO2", "Test from FOO", "This is a test!"));
                Assert.AreEqual("TEST_MAIL_TO2", mail.LoadProperty(o=>o.From).UserName);
                Assert.AreEqual(4, mailService.GetMailboxes().Count()); // Will not have a SENT folder


            }

            // As SYSTEM
            using (AuthenticationContext.EnterSystemContext())
            {
                var mailboxes = mailService.GetMailboxes().Where(o => o.Name == Mailbox.INBOX_NAME).FirstOrDefault();
                var messages = mailService.GetMessages(mailboxes.Key.Value).OrderByDescending(o=>o.DeliveredTime);
                var message = messages.First().LoadProperty(o => o.TargetEntity);
                Assert.GreaterOrEqual(messages.Count(), 1);
                Assert.AreEqual("Test from FOO", message.Subject);
                Assert.AreEqual("TEST_MAIL_TO2", message.LoadProperty(o=>o.From).UserName);
                Assert.IsTrue(message.RcptTo.Any(r => r is SecurityUser su && su.UserName == "SYSTEM"));
                Assert.IsTrue(message.RcptTo.Any(r => r is SecurityUser su && su.UserName == "TEST_MAIL_TO2"));


            }

            // As user test moving
            using (AuthenticationContext.EnterContext(identityService.Authenticate("TEST_MAIL_TO2", "@Foo123!!")))
            {
                // User can create their own mailbox
                var mailboxes = mailService.GetMailboxes();
                Mailbox fooMailbox = mailboxes.FirstOrDefault(o => o.Name == "FOO!"), inbox = mailboxes.FirstOrDefault(o => o.Name == Mailbox.INBOX_NAME);
                var message = mailService.GetMessages(inbox.Key.Value).First();
                Assert.AreEqual(2, mailService.GetMessages(inbox.Key.Value).Count());

                mailService.MoveMessage(inbox.Key.Value, message.TargetEntityKey.Value, fooMailbox.Key.Value);
                Assert.AreEqual(1, mailService.GetMessages(fooMailbox.Key.Value).Count());
                Assert.AreEqual(1, mailService.GetMessages(inbox.Key.Value).Count());

                // Copy and test delete
                mailService.MoveMessage(fooMailbox.Key.Value, message.TargetEntityKey.Value, inbox.Key.Value, true);
                Assert.AreEqual(1, mailService.GetMessages(fooMailbox.Key.Value).Count());
                Assert.AreEqual(2, mailService.GetMessages(inbox.Key.Value).Count());

                // Delete from FOO
                mailService.DeleteMessage(fooMailbox.Key.Value, message.TargetEntityKey.Value);
                Assert.AreEqual(0, mailService.GetMessages(fooMailbox.Key.Value).Count());
                Assert.AreEqual(2, mailService.GetMessages(inbox.Key.Value).Count());

                // Delete the FOO mailbox
                mailService.DeleteMailbox(fooMailbox.Key.Value);
                Assert.AreEqual(3, mailService.GetMailboxes().Count());

            }
        }



    }
}
