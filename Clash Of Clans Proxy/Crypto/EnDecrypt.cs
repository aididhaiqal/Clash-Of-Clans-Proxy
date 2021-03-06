﻿using System;
using System.Linq;
using System.Text;
using Blake2Sharp;
using System.IO;
using Ionic.Zlib;

namespace ClashOfClansProxy
{
    class EnDecrypt
    {
        // Constants
        private const int KeyLength = 32, NonceLength = 24, SessionLength = 24;

        // A custom keypair used for en/decryption 
        private static KeyPair CustomKeyPair = new KeyPair();

        // The 32-byte prefixed public key from cipher 10101
        private static byte[] _10101_PublicKey = new byte[KeyLength];

        // The 24-byte prefixed session key from plain 10101
        private static byte[] _10101_SessionKey = new byte[SessionLength];

        // The 24-byte prefixed nonce from plain 10101
        private static byte[] _10101_Nonce = new byte[NonceLength];

        // The 24-byte prefixed nonce from plain 20103/20104
        private static byte[] _20103_20104_Nonce = new byte[NonceLength];

        // The 32-byte prefixed shared key from plain 20103/20104
        private static byte[] _20103_20104_SharedKey = new byte[KeyLength];

        // Default Blake2b
        private static Hasher Blake2b = Blake2B.Create(new Blake2BConfig() { OutputSizeInBytes = 24 });

        /// <summary>
        /// Encrypts a client/server packet 
        /// </summary>
        /// <param name="p">Packet to encrypt</param>
        /// <returns>Encrypted payload</returns>
        public static byte[] EncryptPacket(Packet p)
        {
            int packetID = p.ID;
            byte[] decryptedPayload = p.DecryptedPayload;
            byte[] encryptedPayload;

            if (packetID == 10100 || packetID == 20100)
            {
                // Both Session (10100) and SessionOk (20100) packets are not encrypted
                // Thus.. just return the decrypted payload
                return decryptedPayload;
            }
            else if (packetID == 10101)
            {
                // The encrypted Login (10101) requires a nonce being calculated by the custom PK & the original PK
                Blake2b.Init();
                Blake2b.Update(CustomKeyPair.PublicKey);
                Blake2b.Update(Keys.OriginalPublicKey);
                var tmpNonce = Blake2b.Finish();

                // The decrypted payload has to be prefixed with the nonce from plain 10101
                decryptedPayload = _10101_SessionKey.Concat(_10101_Nonce).Concat(decryptedPayload).ToArray();
                // Encrypt the payload with the custom NaCl 
                encryptedPayload = CustomNaCl.CreatePublicBox(decryptedPayload, tmpNonce, CustomKeyPair.SecretKey, Keys.OriginalPublicKey);
                // The encrypted payload has to be prefixed with the custom PK
                encryptedPayload = CustomKeyPair.PublicKey.Concat(encryptedPayload).ToArray();
            }
            else if (packetID == 20103 || packetID == 20104)
            {
                // The encrypted LoginFailed / LoginOk (20103/20104) requires a nonce being calculated by the nonce from 10101, the PK from 10101 and the client PK            
                Blake2b.Init();
                Blake2b.Update(_10101_Nonce);
                Blake2b.Update(_10101_PublicKey);
                Blake2b.Update(Keys.ModdedPublicKey);
                var tmpNonce = Blake2b.Finish();

                // The decrypted payload has to be prefixed with the nonce from 20103/20104 and the sharedkey from 20103/20104
                decryptedPayload = _20103_20104_Nonce.Concat(_20103_20104_SharedKey).Concat(decryptedPayload).ToArray();
                // Encrypt the payload with the custom NaCl
                encryptedPayload = CustomNaCl.CreatePublicBox(decryptedPayload, tmpNonce, Keys.GeneratedPrivateKey, _10101_PublicKey);
            }
            else
            {
                // We're dealing with another packet. Depends whether it's a client packet or not.
                if (p.Destination == DataDestination.DATA_FROM_CLIENT)
                {
                    encryptedPayload = CustomNaCl.CreateSecretBox(decryptedPayload, _10101_Nonce, _20103_20104_SharedKey).Skip(16).ToArray();
                }
                else
                {
                    encryptedPayload = CustomNaCl.CreateSecretBox(decryptedPayload, _20103_20104_Nonce, _20103_20104_SharedKey).Skip(16).ToArray();
                }
            }
            return encryptedPayload;
        }

        /// <summary>
        /// Decrypts a packet
        /// </summary>
        public static byte[] DecryptPacket(Packet p)
        {
            int packetID = p.ID;
            byte[] encryptedPayload = p.Payload;
            byte[] decryptedPayload;

            if (packetID == 10100 || packetID == 20100)
            {
                // Both Session (10100) and SessionOk (20100) packets are not encrypted
                // Thus.. just return the encrypted payload
                Logger.LogDecryptedPacket(packetID, encryptedPayload);
                return encryptedPayload;
            }
            else if (packetID == 10101)
            {
                // The decrypted Login (10101) requires a nonce being calculated by the PK & the modded PK
                _10101_PublicKey = encryptedPayload.Take(32).ToArray();
                Blake2b.Init();
                Blake2b.Update(_10101_PublicKey);
                Blake2b.Update(Keys.ModdedPublicKey);
                var tmpNonce = Blake2b.Finish();

                // Decrypt the payload the custom NaCl
                decryptedPayload = CustomNaCl.OpenPublicBox(encryptedPayload.Skip(32).ToArray(), tmpNonce, Keys.GeneratedPrivateKey, _10101_PublicKey);
                _10101_SessionKey = decryptedPayload.Take(24).ToArray();
                _10101_Nonce = decryptedPayload.Skip(24).Take(24).ToArray();
                decryptedPayload = decryptedPayload.Skip(48).ToArray();
                using (var reader = new PacketReader(new MemoryStream(decryptedPayload)))
                {
                    Logger.Log("Packet 10101 Content ->", LogType.PACKET);
                    Console.WriteLine("User ID                      -> " + reader.ReadInt64());
                    Console.WriteLine("User Token                   -> " + reader.ReadString());
                    Console.WriteLine("Major Version                -> " + reader.ReadInt32());
                    Console.WriteLine("Content Version              -> " + reader.ReadInt32());
                    Console.WriteLine("Minor Version                -> " + reader.ReadInt32());
                    Console.WriteLine("MasterHash                   -> " + reader.ReadString());
                    Console.WriteLine("Unknown1                     -> " + reader.ReadString());
                    Console.WriteLine("OpenUDID                     -> " + reader.ReadString());
                    Console.WriteLine("MacAddress                   -> " + reader.ReadString());
                    Console.WriteLine("DeviceModel                  -> " + reader.ReadString());
                    Console.WriteLine("LocaleKey                    -> " + reader.ReadInt32());
                    Console.WriteLine("Language                     -> " + reader.ReadString());
                    Console.WriteLine("AdvertisingGUID              -> " + reader.ReadString());
                    Console.WriteLine("OSVersion                    -> " + reader.ReadString());
                    Console.WriteLine("Unknown2                     -> " + reader.ReadByte());
                    Console.WriteLine("Unknown3                     -> " + reader.ReadString());
                    Console.WriteLine("AndroidDeviceID              -> " + reader.ReadString());
                    Console.WriteLine("FacebookDistributionID       -> " + reader.ReadString());
                    Console.WriteLine("IsAdvertisingTrackingEnabled -> " + reader.ReadBoolean());
                    Console.WriteLine("VendorGUID                   -> " + reader.ReadString());
                    Console.WriteLine("Seed                         -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown4                     -> " + reader.ReadByte());
                    Console.WriteLine("Unknown5                     -> " + reader.ReadString());
                    Console.WriteLine("Unknown6                     -> " + reader.ReadString());
                    Console.WriteLine("ClientVersion                -> " + reader.ReadString());
                }
            }
            else if (packetID == 14102)
            {
                _10101_Nonce.Increment();
                decryptedPayload = CustomNaCl.OpenSecretBox(new byte[16].Concat(encryptedPayload).ToArray(), _10101_Nonce, _20103_20104_SharedKey);
                using (var reader = new PacketReader(new MemoryStream(decryptedPayload)))
                {
                    Logger.Log("Packet 14102 Content ->", LogType.PACKET);
                    Console.WriteLine("Subtick                      -> " + reader.ReadUInt32WithEndian());
                    Console.WriteLine("Checksum                     -> " + reader.ReadUInt32WithEndian());
                    var commandammound = reader.ReadUInt32WithEndian();
                    Console.WriteLine("Command Ammount              -> " + commandammound);
                    if (commandammound > 0 && commandammound < 20)
                    {
                     Console.WriteLine("NestedCommands              -> " +  Encoding.UTF8.GetString(reader.ReadBytes()));
                    }
                }
            }
            else if (packetID == 20103 || packetID == 20104)
            {
                // The decrypted LoginFailed / LoginOk (20103/20104) requires a nonce being calculated by the nonce from 10101, the custom PK & the original PK                   
                Blake2b.Init();
                Blake2b.Update(_10101_Nonce);
                Blake2b.Update(CustomKeyPair.PublicKey);
                Blake2b.Update(Keys.OriginalPublicKey);
                var tmpNonce = Blake2b.Finish();

                // Decrypt the payload with the custom NaCl
                decryptedPayload = CustomNaCl.OpenPublicBox(encryptedPayload, tmpNonce, CustomKeyPair.SecretKey, Keys.OriginalPublicKey);
                _20103_20104_Nonce = decryptedPayload.Take(24).ToArray();
                _20103_20104_SharedKey = decryptedPayload.Skip(24).Take(32).ToArray();
                decryptedPayload = decryptedPayload.Skip(56).ToArray();
                if (packetID == 20104)
                    using (var reader = new PacketReader(new MemoryStream(decryptedPayload)))
                    {
                        Logger.Log("Packet 20104 Content ->", LogType.PACKET);
                        Console.WriteLine("User ID 1                     -> " + reader.ReadInt64());
                        Console.WriteLine("User ID 2                     -> " + reader.ReadInt64());
                        Console.WriteLine("PassToken                     -> " + reader.ReadString());
                        Console.WriteLine("Facebook ID                   -> " + reader.ReadString());
                        Console.WriteLine("GameCenter ID                 -> " + reader.ReadString());
                    }
                else
                    using (var reader = new PacketReader(new MemoryStream(decryptedPayload)))
                    {
                        Logger.Log("Packet 20103 Content ->", LogType.PACKET);
                        Console.WriteLine("Error Code                    -> " + reader.ReadInt32());
                        Console.WriteLine("FingerPrint Data              -> " + reader.ReadString());
                    }
            }
            else if (packetID == 24101)
            {
                _20103_20104_Nonce.Increment();
                decryptedPayload = CustomNaCl.OpenSecretBox(new byte[16].Concat(encryptedPayload).ToArray(), _20103_20104_Nonce, _20103_20104_SharedKey);
                using (var reader = new PacketReader(new MemoryStream(decryptedPayload)))
                {
                    Logger.Log("Packet 24101 Content ->", LogType.PACKET);
                    Console.WriteLine("Last Visit                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 1                     -> " + reader.ReadInt32());
                    Console.WriteLine("TimeStamp                     -> " + DateTimeConverter.FromUnixTimestamp(reader.ReadInt32()));
                    Console.WriteLine("Unknown 2                     -> " + reader.ReadInt32());
                    Console.WriteLine("User ID                       -> " + reader.ReadInt64());
                    Console.WriteLine("Shield Duration               -> " + TimeSpan.FromSeconds(reader.ReadInt32()));
                    Console.WriteLine("Unknown 3                     -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 4                     -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 5                     -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 6                     -> " + reader.ReadInt32());
                    Console.WriteLine("Compressed                    -> " + reader.ReadBoolean());
                    var homeData = reader.ReadBytes();
                    using (var br = new BinaryReader(new MemoryStream(homeData))) // little endian
                    {
                        var decompressedLength = br.ReadInt32();
                        var compressedHome = br.ReadBytes(homeData.Length - 4); // -4 to remove the decompressedLength bytes read
                        Console.WriteLine("Compressed Home             -> " + Encoding.UTF8.GetString(compressedHome));
                        Console.WriteLine("Compressed Home Lenght      -> " + compressedHome.Length);
                        var homeJson = ZlibStream.UncompressString(compressedHome);
                        Console.WriteLine("Decompresssed Lenght        -> " + decompressedLength);
                        Console.WriteLine("Decompressed Home           -> " + homeJson);

                    }
                    Console.WriteLine("Unknown 7                     -> " + reader.ReadInt32());
                    Console.WriteLine("User ID                       -> " + reader.ReadInt64());
                    Console.WriteLine("User ID 2                     -> " + reader.ReadInt64());
                    var haveclan = reader.ReadBoolean();
                    Console.WriteLine("Have Clan                     -> " + haveclan);
                    if (haveclan)
                    {
                        Console.WriteLine("Clan ID                       -> " + reader.ReadInt64());
                        Console.WriteLine("Clan Name                     -> " + reader.ReadString());
                        Console.WriteLine("Clan Badge                    -> " + reader.ReadInt32());
                        Console.WriteLine("Clan Role                     -> " + reader.ReadInt32());
                        Console.WriteLine("Clan Level                    -> " + reader.ReadInt32());
                        Console.WriteLine("Unknown 8                     -> " + reader.ReadBoolean());
                    }
                    Console.WriteLine("Unknown 9                     -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 10                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 11                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 12                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 13                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 14                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 15                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 16                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 17                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 18                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 19                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 20                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 21                    -> " + reader.ReadInt32());
                    Console.WriteLine("League ID                     -> " + reader.ReadInt32());
                    Console.WriteLine("Alliance Castle Level         -> " + reader.ReadInt32());
                    Console.WriteLine("Alliance Castle Total Capacity-> " + reader.ReadInt32());
                    Console.WriteLine("Alliance Castle Used Capacity -> " + reader.ReadInt32());
                    Console.WriteLine("Town Hall Level               -> " + reader.ReadInt32());
                    Console.WriteLine("Name                          -> " + reader.ReadString());
                    Console.WriteLine("Facebook ID                   -> " + reader.ReadInt32());
                    Console.WriteLine("Level                         -> " + reader.ReadInt32());
                    Console.WriteLine("Experience                    -> " + reader.ReadInt32());
                    Console.WriteLine("Gems                          -> " + reader.ReadInt32());
                    Console.WriteLine("FreeGems                      -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 22                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 23                    -> " + reader.ReadInt32());
                    Console.WriteLine("Trophies                      -> " + reader.ReadInt32());
                    Console.WriteLine("Attack Won                    -> " + reader.ReadInt32());
                    Console.WriteLine("Attack Lost                   -> " + reader.ReadInt32());
                    Console.WriteLine("Defences Won                  -> " + reader.ReadInt32());
                    Console.WriteLine("Defences Lost                 -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 24                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 25                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 26                    -> " + reader.ReadInt32());
                    Console.WriteLine("Unknown 27                    -> " + reader.ReadByte());
                    Console.WriteLine("Unknown 28                    -> " + reader.ReadInt64());
                    Console.WriteLine("Name Set                      -> " + reader.ReadBoolean());
                }
            }
            else
            {
                if (p.Destination == DataDestination.DATA_FROM_CLIENT)
                {
                    _10101_Nonce.Increment();
                    decryptedPayload = CustomNaCl.OpenSecretBox(new byte[16].Concat(encryptedPayload).ToArray(), _10101_Nonce, _20103_20104_SharedKey);
                }
                else
                {
                    _20103_20104_Nonce.Increment();
                    decryptedPayload = CustomNaCl.OpenSecretBox(new byte[16].Concat(encryptedPayload).ToArray(), _20103_20104_Nonce, _20103_20104_SharedKey);
                }
            }
            Logger.LogDecryptedPacket(packetID, decryptedPayload);
            return decryptedPayload;
        }
    }
}
