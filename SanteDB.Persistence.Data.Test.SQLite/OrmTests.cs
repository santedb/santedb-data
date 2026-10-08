using NUnit.Framework;
using SanteDB.OrmLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    /// <summary>
    /// ORM Tests
    /// </summary>
    [TestFixture]
    public class OrmTests
    {

        [Test]
        public void TestParseJims1732()
        {
            try
            {
                var sqlStatement = new SqlStatement("SELECT * FROM FOO WHERE id = 'Patient?foo'");
            }
            catch
            {
                Assert.Fail("Question mark in string should not throw");
            }

            // Argument mismatch
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlStatement("SELECT * FROM FOO WHERE id = 'Patient?foo' AND this = ?"));

            try
            {
                new SqlStatement("SELECT * FROM FOO WHERE id = 'Patient?foo' AND this = ?", 'a');
            }
            catch
            {
                Assert.Fail("Parameter has correct number of parameters");
            }

            // Should fail - syntax error - should expect parameter
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlStatement("SELECT * FROM FOO WHERE id = ''Patient?foo' AND this = ?'"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlStatement("SELECT * FROM FOO WHERE id = 'Patient?foo=''this''' AND this = ?"));

            // Should not fail - proper escaping and parameter count
            try
            {
                new SqlStatement("SELECT * FROM FOO WHERE id = 'Patient?foo=''this''' AND this = ?", 'a');
            }
            catch
            {
                Assert.Fail("Parameter has correct number of parameters");
            }
        }
    }
}
