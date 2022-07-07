using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MessageHub.Complement;

public static class PKI
{
    public static X509Certificate2 CreateCertificate(X509Certificate2 issuerCertificate, string subjectName)
    {
        using var key = RSA.Create();
        var certificateRequest = new CertificateRequest(
            $"CN={subjectName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        using var certificate = certificateRequest.Create(
            issuerCertificate,
            issuerCertificate.NotBefore,
            issuerCertificate.NotAfter,
            serialNumber);
        return certificate.CopyWithPrivateKey(key);
    }
}
