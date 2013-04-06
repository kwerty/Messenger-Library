using System;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;

//credit for challenge code goes to the MsnpSharp project - thanks!
//http://msnp-sharp.googlecode.com

namespace MessengerLibrary.Authentication
{

    public static class HandshakeUtility
    {

        public static string GenerateChallengeRespose(string challengeToken, string productID, string productKey)
        {

            MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
            byte[] bMD5Bytes = Encoding.Default.GetBytes(challengeToken + productKey);
            MD5.TransformFinalBlock(bMD5Bytes, 0, bMD5Bytes.Length);

            string strMD5Hash = To_Hex(MD5.Hash);
            ulong[] uMD5Ints = MD5_To_Int(strMD5Hash);

            string strCHLID = challengeToken + productID;
            strCHLID = strCHLID.PadRight(strCHLID.Length + (8 - (strCHLID.Length % 8)), '0');
            ulong[] uCHLIDInts = CHLID_To_Int(strCHLID);

            ulong uKey = Create_Key(uMD5Ints, uCHLIDInts);

            ulong uPartOne = ulong.Parse(strMD5Hash.Substring(0, 16), NumberStyles.HexNumber);
            ulong uPartTwo = ulong.Parse(strMD5Hash.Substring(16, 16), NumberStyles.HexNumber);
            return String.Format("{0:x16}{1:x16}", uPartOne ^ uKey, uPartTwo ^ uKey);
        }

        static ulong[] MD5_To_Int(string strMD5Hash)
        {

            ulong[] uMD5Ints = new ulong[4];

            for (int i = 0; i < strMD5Hash.Length; i += 8)
                uMD5Ints[i / 8] = ulong.Parse(Swap_Bytes(strMD5Hash.Substring(i, 8), 2), NumberStyles.HexNumber) & 0x7FFFFFFF;

            return uMD5Ints;
        }

        static ulong[] CHLID_To_Int(string strCHLID)
        {

            ulong[] uCHLIDInts = new ulong[strCHLID.Length / 4];

            for (int i = 0; i < strCHLID.Length; i += 4)
                uCHLIDInts[i / 4] = ulong.Parse(Swap_Bytes(To_Hex(Encoding.Default.GetBytes(strCHLID.Substring(i, 4))), 2), NumberStyles.HexNumber);

            return uCHLIDInts;
        }

        static ulong Create_Key(ulong[] uMD5Ints, ulong[] uCHLIDInts)
        {

            ulong temp = 0, high = 0, low = 0;
            for (int i = 0; i < uCHLIDInts.Length; i += 2)
            {
                temp = ((uCHLIDInts[i] * 0x0E79A9C1) % 0x7FFFFFFF) + high;
                temp = ((temp * uMD5Ints[0]) + uMD5Ints[1]) % 0x7FFFFFFF;

                high = (uCHLIDInts[i + 1] + temp) % 0x7FFFFFFF;
                high = ((high * uMD5Ints[2]) + uMD5Ints[3]) % 0x7FFFFFFF;

                low += high + temp;
            }

            high = ulong.Parse(Swap_Bytes(String.Format("{0:x8}", (high + uMD5Ints[1]) % 0x7FFFFFFF), 2), NumberStyles.HexNumber);
            low = ulong.Parse(Swap_Bytes(String.Format("{0:x8}", (low + uMD5Ints[3]) % 0x7FFFFFFF), 2), NumberStyles.HexNumber);

            return (high << 32) + low;
        }

        static string To_Hex(byte[] bBinary)
        {
            string strHex = String.Empty;
            foreach (byte i in bBinary)
                strHex += Convert.ToString(i, 16).PadLeft(2, '0');

            return strHex;
        }

        static string Swap_Bytes(string strString, int iStep)
        {

            string strNewString = String.Empty;
            for (int i = 0; i < strString.Length; i += iStep)
                strNewString = strString.Substring(i, iStep) + strNewString;

            return strNewString;
        }

    }

}














