// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport
{
    public abstract class LocalDatabaseContext : ContextBase
    {
        protected const string LocalDbConnectionString = @"Data Source=(LocalDB)\v11.0;Initial Catalog=master;Integrated Security=True";

        protected string dbFileName;
        protected string dbLogFileName;

        protected string dbName;
        protected SqlConnection localDbConnection = new SqlConnection(LocalDbConnectionString);

        protected abstract string GetLocalDatabaseFileName();

        protected override void Given()
        {
            this.dbName = this.GetLocalDatabaseFileName();

            if (string.IsNullOrWhiteSpace(dbName))
            {
                Assert.Inconclusive("You must specify a valid database name");
            }

            var output = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.dbFileName = Path.Combine(output, dbName + ".mdf");
            this.dbLogFileName = Path.Combine(output, string.Format("{0}_log.ldf", dbName));

            this.localDbConnection.Open();

            // Recover from failed run
            this.DetachDatabase();

            File.Delete(this.dbFileName);
            File.Delete(this.dbLogFileName);

            using (var cmd = new SqlCommand(string.Format("CREATE DATABASE {0} ON (NAME = N'{0}', FILENAME = '{1}')", this.dbName, this.dbFileName), this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        protected override void OnCleanup()
        {
            using (var cmd = new SqlCommand(string.Format("ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", this.dbName), this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }

            this.localDbConnection.ChangeDatabase("master");
            this.DetachDatabase();
            this.localDbConnection.Dispose();

            File.Delete(this.dbFileName);
            File.Delete(this.dbLogFileName);
        }

        protected string GetSqlConnectionString()
        {
            var cs = string.Format(@"Data Source=(LocalDB)\v11.0;AttachDBFileName={1};Initial Catalog={0};Integrated Security=True;", this.dbName, this.dbFileName);

            return cs;
        }

        protected void DetachDatabase()
        {
            using (var cmd = new SqlCommand(string.Format("IF EXISTS (SELECT * FROM sys.databases WHERE Name = N'{0}') exec sp_detach_db N'{0}'", dbName), this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
