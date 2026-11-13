// SHA-256 compression logic adapted from Brad Conte's public-domain implementation:
// https://github.com/B-Con/crypto-algorithms/blob/master/sha256.c
// See THIRD_PARTY_NOTICES.md for details

using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

namespace First.GpuMining {
    public static class GpuMineKernel {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Rotr(uint x, int n) => (x >> n) | (x << (32 - n));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Ch(uint x, uint y, uint z) => (x & y) ^ (~x & z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Maj(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Ep0(uint x) => Rotr(x, 2) ^ Rotr(x, 13) ^ Rotr(x, 22);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Ep1(uint x) => Rotr(x, 6) ^ Rotr(x, 11) ^ Rotr(x, 25);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Sig0(uint x) => Rotr(x, 7) ^ Rotr(x, 18) ^ (x >> 3);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Sig1(uint x) => Rotr(x, 17) ^ Rotr(x, 19) ^ (x >> 10);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Compress(ArrayView<uint> k, uint[] w, uint[] s) {
            uint a = s[0], b = s[1], c = s[2], d = s[3];
            uint e = s[4], f = s[5], g = s[6], h = s[7];

            for (int i = 0; i < 64; ++i) {
                uint wi;
                if (i < 16) wi = w[i];
                else {
                    wi = Sig1(w[(i - 2) & 15]) + w[(i - 7) & 15]
                       + Sig0(w[(i - 15) & 15]) + w[(i - 16) & 15];
                    w[i & 15] = wi;
                }
                uint t1 = h + Ep1(e) + Ch(e, f, g) + k[i] + wi;
                uint t2 = Ep0(a) + Maj(a, b, c);
                h = g; g = f; f = e; e = d + t1;
                d = c; c = b; b = a; a = t1 + t2;
            }

            s[0] += a; s[1] += b; s[2] += c; s[3] += d;
            s[4] += e; s[5] += f; s[6] += g; s[7] += h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint ReadBE(byte[] b, int o) =>
            ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FormatDecimal(ulong value, byte[] buf, int offset) {
            if (value == 0) {
                buf[offset] = (byte)'0';
                return 1;
            }

            byte[] tmp = new byte[20];
            int len = 0;
            while (value > 0) {
                tmp[len++] = (byte)('0' + (int)(value % 10));
                value /= 10;
            }

            for (int i = 0; i < len; ++i)
                buf[offset + i] = tmp[len - 1 - i];
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void LoadMidstate(uint[] s, ArrayView<uint> midstate) {
            for (int i = 0; i < 8; ++i) s[i] = midstate[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MeetsTarget(uint[] s, ArrayView<uint> target) {
            for (int i = 0; i < 8; ++i) {
                if (s[i] < target[i]) return true;
                if (s[i] > target[i]) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ReportHit(ArrayView<uint> result, ulong nonce, uint[] s) {
            if (Atomic.CompareExchange(ref result[0], 0u, 1u) == 0u) {
                result[1] = (uint)(nonce & 0xFFFFFFFF);
                result[2] = (uint)(nonce >> 32);
                for (int i = 0; i < 8; ++i) result[3 + i] = s[i];
            }
        }

        /// <summary>
        /// Hashes prefix + decimal(nonce) for one nonce. The prefix is split on
        /// the CPU into a midstate (all complete 64-byte blocks) and a tail
        /// (0..63 bytes). Only the final 1 or 2 blocks are processed here.
        /// </summary>
        public static void Mine(
            Index1D idx,
            ArrayView<uint> midstate,   // 8 words
            ArrayView<byte> tail,       // up to 63 bytes
            int tailLen,
            uint prefixLen,
            ArrayView<uint> k,          // 64 round constants
            ArrayView<uint> target,     // 8 big-endian words
            ulong startNonce,
            ArrayView<uint> result)     // [0]=found, [1..2]=nonce, [3..10]=digest
        {
            ulong nonce = startNonce + (ulong)idx;

            byte[] buf = new byte[128];
            for (int i = 0; i < tailLen; ++i)
                buf[i] = tail[i];

            int digits = FormatDecimal(nonce, buf, tailLen);
            int m = tailLen + digits;
            ulong totalLen = (ulong)prefixLen + (ulong)digits;

            buf[m] = 0x80;
            int padded = ((m + 9 + 63) / 64) * 64;
            for (int i = m + 1; i < padded - 8; ++i)
                buf[i] = 0;

            ulong bits = totalLen * 8;
            int lenPos = padded - 8;
            for (int i = 0; i < 8; ++i)
                buf[lenPos + i] = (byte)(bits >> (56 - 8 * i));

            uint[] s = new uint[8];
            LoadMidstate(s, midstate);

            uint[] w = new uint[16];
            int blocks = padded / 64;
            for (int b = 0; b < blocks; ++b) {
                int off = b * 64;
                for (int i = 0; i < 16; ++i)
                    w[i] = ReadBE(buf, off + i * 4);
                Compress(k, w, s);
            }

            if (MeetsTarget(s, target))
                ReportHit(result, nonce, s);
        }
    }
}
