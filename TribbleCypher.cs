﻿#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2017 Babbacombe Computers Ltd.

    This class was provided under LGPL by Green Box Intelligence Ltd
    https://github.com/Rushyo
    
    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
    USA
 */
#endregion
using System;
using System.Linq;
using System.Security.Cryptography;
using Babbacombe.SockLib;
using System.Threading;

namespace TribbleCipher {
    public class Tribble<T> : SymmetricAlgorithm, ICryptoTransform where T : HashAlgorithm {
        private Int64 _counter;
        private Byte _position;
        private readonly HashAlgorithm _hash;
        private Byte[] _state;
        private Byte[] _key;

        protected Tribble(Byte[] key, T hash, Int64 counter, Byte position, Byte[] state) {
            _position = position;
            _counter = counter;
            _hash = hash;
            _state = state.ToArray();
            _key = key.ToArray();
        }

        public Tribble(Byte[] key, T hash) {
            if (typeof(T).IsSubclassOf(typeof(MD5)))
                throw new NotSupportedException(@"Really? MD5?!? NO.");
            if (hash == null)
                throw new ArgumentNullException(@"hash");
            if (key == null || key.Length != (hash.HashSize / 8))
                throw new ArgumentException(@"Invalid key", @"key");
            _hash = hash;
            _state = key.ToArray();
            _key = key.ToArray();
            Next();
        }

        internal void Next() {
            Byte[] counterBytes = BitConverter.GetBytes(_counter);
            for (var i = 0; i < counterBytes.Length; i++)
                _state[i] ^= counterBytes[i];
            _state = _hash.ComputeHash(_state);
            _counter++;
            _position = 0;
        }

        public Byte[] XOR(Byte[] input) {
            var output = new Byte[input.Length];
            for (var i = 0; i < input.Length; i++) {
                output[i] = (Byte)(input[i] ^ _state[_position]);
                _position++;
                if (_position % (_hash.HashSize / 8) == 0)
                    Next();
            }
            return output;
        }

        internal void Reset() {
            _counter = 0;
            _position = 0;
            _state = _key.ToArray();
            Next();
        }

        public new void Dispose() {
            _hash.Dispose();
        }

        public Boolean CanReuseTransform {
            get { return true; }
        }

        public Boolean CanTransformMultipleBlocks {
            get { return true; }
        }

        public Int32 InputBlockSize {
            get { return 1; }
        }

        public Int32 OutputBlockSize {
            get { return 1; }
        }

        public Int32 TransformBlock(Byte[] inputBuffer, Int32 inputOffset, Int32 inputCount, Byte[] outputBuffer, Int32 outputOffset) {
            Byte[] transformed = XOR(inputBuffer.Skip(inputOffset).Take(inputCount).ToArray());
            Array.Copy(transformed, 0, outputBuffer, outputOffset, inputCount);
            return inputCount;
        }

        public Byte[] TransformFinalBlock(Byte[] inputBuffer, Int32 inputOffset, Int32 inputCount) {
            return XOR(inputBuffer.Skip(inputOffset).Take(inputCount).ToArray());
        }

        public override ICryptoTransform CreateDecryptor() {
            return CreateDecryptor(_key, null);
        }

        public override ICryptoTransform CreateDecryptor(Byte[] rgbKey, Byte[] rgbIV) {
            HashAlgorithm hashAlgorithm = HashAlgorithm.Create(typeof(T).Name);
            if (hashAlgorithm == null)
                throw new InvalidOperationException(@"Unknown hash algorithm");
            if (rgbKey.Length != hashAlgorithm.HashSize / 8)
                throw new ArgumentException("Invalid key", @"rgbKey");
            return new Tribble<T>(rgbKey, (T)hashAlgorithm, _counter, _position, _state);
        }

        public override ICryptoTransform CreateEncryptor() {
            return CreateDecryptor();
        }

        public override ICryptoTransform CreateEncryptor(Byte[] rgbKey, Byte[] rgbIV) {
            return CreateDecryptor(rgbKey, rgbIV);
        }

        public override void GenerateIV() {
            throw new NotImplementedException("IV is not used by this cipher");
        }

        public override void GenerateKey() {
            HashAlgorithm hashAlgorithm = HashAlgorithm.Create(typeof(T).Name);
            if (hashAlgorithm == null)
                throw new InvalidOperationException(@"Unknown hash algorithm");
            Int32 bytes = hashAlgorithm.HashSize / 8;
            var key = new Byte[bytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(key);
            _key = key;
            Reset();
        }
    }

}