using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Harry.Utility
{
    public class Crypto
    {
        private string first = "1";
        private string last = "1";

        public byte[] Encrypt(string content)
        {
            using var aesAlg = Aes.Create();
            using var encryptor = aesAlg.CreateEncryptor(first.ToBytes(), last.ToBytes());
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(content);
            }
            return msEncrypt.ToArray();
        }

        public string Decrypt(byte[] bytes)
        {
            using var aesAlg = Aes.Create();
            using var decryptor = aesAlg.CreateDecryptor(first.ToBytes(), last.ToBytes());
            using var msDecrypt = new MemoryStream(bytes);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            return srDecrypt.ReadToEnd();
        }

        public byte[] GetHash(string inputString)
        {
            using var algorithm = SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }
    }
}
