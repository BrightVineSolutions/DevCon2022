using System;
using System.IO;
using Blackbaud.AppFx.Server;
using System.Data.SqlClient;
using System.Data;

internal sealed class ImpersonationHelper
{
    internal static UserImpersonationScope GetImpersonationScope(string usernameAndDomain, string password)
    {
        // Using a static impersonationscope so the object can be reused and not create extra items as this is needed thoughout the code
        var domain = string.Empty;
        var userName = string.Empty;

        ParseDomainAndUserName(usernameAndDomain, ref domain, ref userName);

        return new UserImpersonationScope(userName, domain, password);
    }

    private static void ParseDomainAndUserName(string domainAndUserName, ref string domain, ref string userName)
    {
        var indexOfSlash = domainAndUserName.IndexOf(@"\", StringComparison.CurrentCulture);

        if (indexOfSlash > 0)
        {
            domain = domainAndUserName.Substring(0, indexOfSlash);
            userName = domainAndUserName.Remove(0, domain.Length + 1);
        }
        else
        {
            domain = string.Empty;
            userName = domainAndUserName;
        }
    }

    internal static void GrantRightsToFileForImportUser(string impersonateUserName, string filePath)
    {
        string domain = null;
        string userName = null;

        ParseDomainAndUserName(impersonateUserName, ref domain, ref userName);

        System.Security.Principal.NTAccount importUserAccount = new System.Security.Principal.NTAccount(domain, userName);

        System.Security.AccessControl.FileSystemAccessRule rule = new System.Security.AccessControl.FileSystemAccessRule(importUserAccount, System.Security.AccessControl.FileSystemRights.Read | System.Security.AccessControl.FileSystemRights.Write | System.Security.AccessControl.FileSystemRights.Delete, System.Security.AccessControl.AccessControlType.Allow);

        System.Security.AccessControl.FileSecurity fileSecurity = File.GetAccessControl(filePath);
        fileSecurity.AddAccessRule(rule);

        File.SetAccessControl(filePath, fileSecurity);
    }

    internal static Blackbaud.AppFx.SpWrap.USP_IMPORTPROCESSOPTIONS_GET.ResultRow GetImportImpersonationOptions(string connString)
    {
        int retValue = 0;
        Blackbaud.AppFx.SpWrap.USP_IMPORTPROCESSOPTIONS_GET.ResultRow result = null;

        using (SqlConnection conn = new SqlConnection(connString))
        {
            conn.Open();

            result = Blackbaud.AppFx.SpWrap.USP_IMPORTPROCESSOPTIONS_GET.WrapperRoutines.ExecuteRow(conn, ref retValue);

            if (conn.State == ConnectionState.Open)
            conn.Close();
        }

        return result;
    }

    internal static FileStream GetImportReadFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    internal static FileStream GetImportWriteFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
    }
}
