using System;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Permissions;

namespace Nullfactory.SSRSExtensions
{
    /// <summary>
    /// Helper class to retrieve a connection string stored in a SharePoint List
    /// </summary>
    /// <remarks>
    /// It is assumed that the SharePoint server is the same as the report server url.
    /// </remarks>
    public class SPListConnectionStringHelper
    {
        /// <summary>
        /// Default Constructor
        /// </summary>
        public SPListConnectionStringHelper()
        {

        }


        /// <summary>
        /// Get the connection string
        /// </summary>
        /// <param name="reportServerUrl">The Report Server Url</param>
        /// <returns>Plain text connections tring</returns>
        [SecuritySafeCritical]
        public string GetConnectionString(string reportServerUrl)
        {
            //assumption that the configuration list would be on the same sharepoint server
            //change as required

            Uri serverUri = new Uri(reportServerUrl);
            string serverUrl = serverUri.GetLeftPart(UriPartial.Authority);

            var credential = GetSecurityCredentials();

            string encryptedConnectionString = this.GetConnectionStringFromSharePointList(serverUrl, credential);

            return this.DecryptConnectionString(encryptedConnectionString);
        }

        /// <summary>
        /// Get encrypted connection string from SharePoint list
        /// </summary>
        /// <param name="reportServerUrl">Report Server Url</param>
        /// <param name="credentials">The Credentials</param>
        /// <returns>The connection string</returns>
        [SecurityCritical]
        internal string GetConnectionStringFromSharePointList(string reportServerUrl, ICredentials credentials)
        {
            //assumption that the connection string to the database is stored as an encrypted value in a list called "Configuration"
            string webRequestUrl = 
                string.Format("{0}/_api/lists/getbytitle('Configuration')/items/?$select=Title,Value&$filter=startswith(Title,'ConnectionString')", reportServerUrl);

            var webPermission = new System.Net.WebPermission(NetworkAccess.Connect, webRequestUrl);
            webPermission.Assert();


            var endpointRequest = (HttpWebRequest)HttpWebRequest.Create(webRequestUrl);

            endpointRequest.Method = "GET";
            endpointRequest.Accept = "application/json;odata=verbose";
            endpointRequest.Credentials = credentials;

            var endpointResponse = (HttpWebResponse)endpointRequest.GetResponse();
            try
            {
                WebResponse webResponse = endpointRequest.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream())
                {
                    using (StreamReader responseReader = new StreamReader(webStream))
                    {
                        try
                        {
                            string response = responseReader.ReadToEnd();
                            var connectionString = ExtractConnectionStringFromJSON(response);

                            return connectionString;
                        }
                        finally
                        {
                            responseReader.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Error when: GetFromSharePointConfigurationList:", e);
            }
        }


        /// <summary>
        /// Get the Security Asserted Default Credentials
        /// </summary>
        /// <returns>CredentialCache.DefaultCredentials</returns>
        [SecuritySafeCritical()]
        internal ICredentials GetSecurityCredentials()
        {
            EnvironmentPermission permission = 
                new EnvironmentPermission(EnvironmentPermissionAccess.Read, "USERNAME");

            permission.Assert();
            return CredentialCache.DefaultCredentials;
        }


        /// <summary>
        /// Decrypt the connection string
        /// </summary>
        /// <param name="encryptedString">Encrypted connection string</param>
        /// <returns>Decrypted connection string</returns>
        public string DecryptConnectionString(string encryptedString)
        {
            
            #warning implement your own decryption logic here

            //my decryption logic assumes that the string is reversed

            char[] charArray = encryptedString.ToCharArray();
            Array.Reverse( charArray );
            return new string( charArray );
        }

        
        /// <summary>
        /// Extract the connection string from JSON resultset
        /// </summary>
        /// <param name="resultSet">JSON resultset</param>
        /// <returns>The extracted connection string</returns>
        private string ExtractConnectionStringFromJSON(string resultSet)
        {
            //ugly code but gets the job done. Plus no need a dependency on an external JSON library

            try
            {            
                int startX = resultSet.IndexOf("\"Value\":\"");
                startX += 9;
                int endX = resultSet.IndexOf("\"}]}}", startX);

                string value = resultSet.Substring(startX, (endX - startX));

                return value;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
