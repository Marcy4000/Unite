using System;
using UnityEngine;

public struct PlayerClothesInfo : IEquatable<PlayerClothesInfo>
{
    // Exposed as ushorts externally; stored packed into 72 bits when serialized.
    public ushort Hat;
    public ushort Eyes;     // Exposed for API parity but not stored (unused in packed bytes)
    public ushort Shirt;
    public ushort Overwear; // Stored as 5 bits (wraps on serialize)
    public ushort Gloves;
    public ushort Pants;
    public ushort Socks;
    public ushort Shoes;
    public ushort Backpack;

    private byte _faceAndHair; // Encodes IsMale (1 bit), Face (3 bits), and Hair (4 bits)

    public Color32 HairColor;
    public Color32 EyeColor;
    public byte SkinColor;

    public TrainerCardInfo TrainerCardInfo;

    public bool IsMale
    {
        get => (_faceAndHair & 0x80) != 0; // MSB is IsMale
        set
        {
            if (value)
                _faceAndHair |= 0x80; // Set MSB
            else
                _faceAndHair &= 0x7F; // Clear MSB
        }
    }

    public byte Face
    {
        get => (byte)((_faceAndHair & 0x70) >> 4); // Bits 6-4
        set
        {
            if (value > 7) throw new ArgumentOutOfRangeException(nameof(value), "Face index must be between 0 and 7.");
            _faceAndHair = (byte)((_faceAndHair & 0x8F) | (value << 4)); // Mask bits 6-4 and set new value
        }
    }

    public byte Hair
    {
        get => (byte)(_faceAndHair & 0x0F); // Bits 3-0
        set
        {
            if (value > 15) throw new ArgumentOutOfRangeException(nameof(value), "Hair index must be between 0 and 15.");
            _faceAndHair = (byte)((_faceAndHair & 0xF0) | value); // Mask bits 3-0 and set new value
        }
    }

    // Bit helpers (LSB-first)
    static void WriteBits(byte[] buf, ref int bitPos, ulong value, int bits)
    {
        ulong mask = bits >= 64 ? ulong.MaxValue : ((1UL << bits) - 1UL);
        value &= mask;
        for (int i = 0; i < bits; i++)
        {
            int byteIndex = bitPos >> 3;
            int bitIndex = bitPos & 7;
            if (((value >> i) & 1UL) != 0UL) buf[byteIndex] |= (byte)(1 << bitIndex);
            bitPos++;
        }
    }

    static ulong ReadBits(byte[] buf, ref int bitPos, int bits)
    {
        ulong value = 0;
        for (int i = 0; i < bits; i++)
        {
            int byteIndex = bitPos >> 3;
            int bitIndex = bitPos & 7;
            if ((buf[byteIndex] & (1 << bitIndex)) != 0) value |= (1UL << i);
            bitPos++;
        }
        return value;
    }

    // Exposed as ushort to callers; stored values may wrap based on internal bit width.
    public ushort GetClothingIndex(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hat: return Hat;
            case ClothingType.Hair: return Hair;
            case ClothingType.Face: return Face;
            case ClothingType.Eyes: return Eyes;
            case ClothingType.Shirt: return Shirt;
            case ClothingType.Overwear: return Overwear;
            case ClothingType.Gloves: return Gloves;
            case ClothingType.Pants: return Pants;
            case ClothingType.Socks: return Socks;
            case ClothingType.Shoes: return Shoes;
            case ClothingType.Backpack: return Backpack;
            default: throw new ArgumentException("Invalid clothing type.");
        }
    }

    public void SetClothingItem(ClothingType type, ushort index)
    {
        switch (type)
        {
            case ClothingType.Hat: Hat = index; break;
            case ClothingType.Hair: Hair = (byte)index; break;
            case ClothingType.Face: Face = (byte)index; break;
            case ClothingType.Eyes: Eyes = index; break;
            case ClothingType.Shirt: Shirt = index; break;
            case ClothingType.Overwear: Overwear = index; break;
            case ClothingType.Gloves: Gloves = index; break;
            case ClothingType.Pants: Pants = index; break;
            case ClothingType.Socks: Socks = index; break;
            case ClothingType.Shoes: Shoes = index; break;
            case ClothingType.Backpack: Backpack = index; break;
            default: throw new ArgumentException("Invalid clothing type.");
        }
    }

    public string Serialize()
    {
        byte[] data = new byte[26];

        // Pack fields into 72-bit buffer (9 bytes). LSB-first ordering.
        byte[] packed = new byte[9];
        int bitPos = 0;
        WriteBits(packed, ref bitPos, Hat, 10);      // bits 0..9
        WriteBits(packed, ref bitPos, Shirt, 12);    // bits 10..21
        WriteBits(packed, ref bitPos, Overwear, 5);  // bits 22..26
        WriteBits(packed, ref bitPos, Gloves, 9);    // bits 27..35
        WriteBits(packed, ref bitPos, Pants, 9);     // bits 36..44
        WriteBits(packed, ref bitPos, Socks, 9);     // bits 45..53
        WriteBits(packed, ref bitPos, Shoes, 9);     // bits 54..62
        WriteBits(packed, ref bitPos, Backpack, 9);  // bits 63..71

        // Map packed bytes into the original physical byte layout while keeping _faceAndHair at data[1]
        data[0] = packed[0];
        data[1] = _faceAndHair;
        data[2] = packed[1];
        data[3] = packed[2];
        data[4] = packed[3];
        data[5] = packed[4];
        data[6] = packed[5];
        data[7] = packed[6];
        data[8] = packed[7];

        data[9] = HairColor.r;
        data[10] = HairColor.g;
        data[11] = HairColor.b;
        data[12] = EyeColor.r;
        data[13] = EyeColor.g;
        data[14] = EyeColor.b;
        data[15] = SkinColor;
        data[16] = packed[8];

        TrainerCardInfo.Serialize().CopyTo(data, 17);

        return Convert.ToBase64String(data);
    }

    public static PlayerClothesInfo Deserialize(string data)
    {
        byte[] bytes = Convert.FromBase64String(data);

        if (bytes.Length == 16) // Legacy/new-small format - preserve original mapping
        {
            return new PlayerClothesInfo
            {
                Hat = bytes[0],
                _faceAndHair = bytes[1], // Encoded IsMale, Face, and Hair
                Eyes = bytes[2],
                Shirt = bytes[3],
                Overwear = bytes[4],
                Gloves = bytes[5],
                Pants = bytes[6],
                Socks = bytes[7],
                Shoes = bytes[8],
                HairColor = new Color32(bytes[9], bytes[10], bytes[11], 255),
                EyeColor = new Color32(bytes[12], bytes[13], bytes[14], 255),
                SkinColor = bytes[15]
            };
        }
        else if (bytes.Length == 26)
        {
            // Rebuild packed 9-byte buffer from the original physical layout:
            byte[] packed = new byte[9];
            packed[0] = bytes[0];
            packed[1] = bytes[2];
            packed[2] = bytes[3];
            packed[3] = bytes[4];
            packed[4] = bytes[5];
            packed[5] = bytes[6];
            packed[6] = bytes[7];
            packed[7] = bytes[8];
            packed[8] = bytes[16];

            int bitPos = 0;
            var info = new PlayerClothesInfo();
            info.Hat = (ushort)ReadBits(packed, ref bitPos, 10);
            info.Shirt = (ushort)ReadBits(packed, ref bitPos, 12);
            info.Overwear = (ushort)ReadBits(packed, ref bitPos, 5);
            info.Gloves = (ushort)ReadBits(packed, ref bitPos, 9);
            info.Pants = (ushort)ReadBits(packed, ref bitPos, 9);
            info.Socks = (ushort)ReadBits(packed, ref bitPos, 9);
            info.Shoes = (ushort)ReadBits(packed, ref bitPos, 9);
            info.Backpack = (ushort)ReadBits(packed, ref bitPos, 9);

            info._faceAndHair = bytes[1];
            info.HairColor = new Color32(bytes[9], bytes[10], bytes[11], 255);
            info.EyeColor = new Color32(bytes[12], bytes[13], bytes[14], 255);
            info.SkinColor = bytes[15];

            byte[] trainerCardData = new byte[9];
            Array.Copy(bytes, 17, trainerCardData, 0, 9);
            info.TrainerCardInfo = TrainerCardInfo.Deserialize(trainerCardData);

            return info;
        }
        else
        {
            throw new ArgumentException("Invalid data length. Expected 16 or 26 bytes.");
        }
    }

    public bool Equals(PlayerClothesInfo other)
    {
        return Hat == other.Hat &&
               _faceAndHair == other._faceAndHair &&
               Eyes == other.Eyes &&
               Shirt == other.Shirt &&
               Overwear == other.Overwear &&
               Gloves == other.Gloves &&
               Pants == other.Pants &&
               Socks == other.Socks &&
               Shoes == other.Shoes &&
               HairColor.Equals(other.HairColor) &&
               EyeColor.Equals(other.EyeColor) &&
               SkinColor == other.SkinColor &&
               Backpack == other.Backpack;
    }
}
