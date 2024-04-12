using System.ComponentModel.DataAnnotations;

namespace DailyBudgetAPI.Models
{
    public class OTP
    {
        [Key]
        public int OTPID { get; set; }
        public string OTPCode { get; set; }
        public DateTime OTPExpiryTime { get; set; }
        public int UserAccountID { get; set; }
        public bool IsValidated { get; set; }
        public string OTPType { get; set; }
    }

    public static class BetterRandom
    {
        private static readonly ThreadLocal<System.Security.Cryptography.RandomNumberGenerator> crng = new ThreadLocal<System.Security.Cryptography.RandomNumberGenerator>(System.Security.Cryptography.RandomNumberGenerator.Create);
        private static readonly ThreadLocal<byte[]> bytes = new ThreadLocal<byte[]>(() => new byte[sizeof(int)]);
        public static int NextInt()
        {
            crng.Value.GetBytes(bytes.Value);
            return BitConverter.ToInt32(bytes.Value, 0) & int.MaxValue;
        }
        public static double NextDouble()
        {
            while (true)
            {
                long x = NextInt() & 0x001FFFFF;
                x <<= 31;
                x |= (long)NextInt();
                double n = x;
                const double d = 1L << 52;
                double q = n / d;
                if (q != 1.0)
                    return q;
            }
        }
    }
}
