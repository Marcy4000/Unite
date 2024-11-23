using System;
using UnityEngine;

public struct PlayerClothesInfo : IEquatable<PlayerClothesInfo>
{
    public byte Hat;
    public byte Eyes;
    public byte Shirt;
    public byte Overwear;
    public byte Gloves;
    public byte Pants;
    public byte Socks;
    public byte Shoes;

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

    public byte GetClothingIndex(ClothingType type)
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
            default: throw new ArgumentException("Invalid clothing type.");
        }
    }

    public void SetClothingItem(ClothingType type, byte index)
    {
        switch (type)
        {
            case ClothingType.Hat: Hat = index; break;
            case ClothingType.Hair: Hair = index; break;
            case ClothingType.Face: Face = index; break;
            case ClothingType.Eyes: Eyes = index; break;
            case ClothingType.Shirt: Shirt = index; break;
            case ClothingType.Overwear: Overwear = index; break;
            case ClothingType.Gloves: Gloves = index; break;
            case ClothingType.Pants: Pants = index; break;
            case ClothingType.Socks: Socks = index; break;
            case ClothingType.Shoes: Shoes = index; break;
            default: throw new ArgumentException("Invalid clothing type.");
        }
    }

    public string Serialize()
    {
        byte[] data = new byte[25];
        data[0] = Hat;
        data[1] = _faceAndHair; // Encodes Face, Hair, and IsMale
        data[2] = Eyes;
        data[3] = Shirt;
        data[4] = Overwear;
        data[5] = Gloves;
        data[6] = Pants;
        data[7] = Socks;
        data[8] = Shoes;
        data[9] = HairColor.r;
        data[10] = HairColor.g;
        data[11] = HairColor.b;
        data[12] = EyeColor.r;
        data[13] = EyeColor.g;
        data[14] = EyeColor.b;
        data[15] = SkinColor;

        TrainerCardInfo.Serialize().CopyTo(data, 16);

        return Convert.ToBase64String(data);
    }

    public static PlayerClothesInfo Deserialize(string data)
    {
        byte[] bytes = Convert.FromBase64String(data);

        if (bytes.Length == 17) // Handle old format
        {
            return new PlayerClothesInfo
            {
                Hat = bytes[0],
                _faceAndHair = (byte)((bytes[2] & 0x7F) << 4 | (bytes[1] & 0x0F)), // Combine Face and Hair
                Eyes = bytes[3],
                Shirt = bytes[4],
                Overwear = bytes[5],
                Gloves = bytes[6],
                Pants = bytes[7],
                Socks = bytes[8],
                Shoes = bytes[9],
                HairColor = new Color32(bytes[10], bytes[11], bytes[12], 255),
                EyeColor = new Color32(bytes[13], bytes[14], bytes[15], 255),
                SkinColor = bytes[16],
                IsMale = (bytes[2] & 0x80) != 0 // Old IsMale from Shoes' MSB
            };
        }
        else if (bytes.Length == 16) // New format
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
        else if (bytes.Length == 25)
        {
            byte[] trainerCardData = new byte[9];
            Array.Copy(bytes, 16, trainerCardData, 0, 9);

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
                SkinColor = bytes[15],
                TrainerCardInfo = TrainerCardInfo.Deserialize(trainerCardData)
            };
        }
        else
        {
            throw new ArgumentException("Invalid data length. Expected 16 or 17 bytes.");
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
               SkinColor == other.SkinColor;
    }
}
