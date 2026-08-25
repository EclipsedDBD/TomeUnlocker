using System;
using System.IO;

namespace TomeUnlocker.Classes
{
    public static class Certificate
    {
        private static readonly string CertPassword = "debecb6dfd034ae3aafc6774049eca52";
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TomeUnlocker");

        public static string CertLocation => Path.Combine(AppDataFolder, "Certs", "TomeUnlocker.p12");

        public static void EnsureCertificate()
        {
            var certDir = Path.GetDirectoryName(CertLocation);
            if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
                Directory.CreateDirectory(certDir);

            var bC = new BCCertMaker.BCCertMaker();
            Fiddler.CertMaker.oCertProvider = bC;

            if (!File.Exists(CertLocation))
            {
                bC.CreateRootCertificate();
                bC.WriteRootCertificateAndPrivateKeyToPkcs12File(CertLocation, CertPassword);
            }
            else
            {
                bC.ReadRootCertificateAndPrivateKeyFromPkcs12File(CertLocation, CertPassword);
            }

            if (!Fiddler.CertMaker.rootCertIsTrusted())
            {
                Fiddler.CertMaker.trustRootCert();
            }
        }
    }
}
