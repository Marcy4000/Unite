using System;
using UnityEngine;

public struct PlayerClothesInfo : IEquatable<PlayerClothesInfo>
{
    public byte Hat;
    public byte Hair;
    public byte Face;
    public byte Eyes;
    public byte Shirt;
    public byte Overwear;
    public byte Gloves;
    public byte Pants;
    public byte Socks;

    private byte _shoes;

    public Color32 HairColor;
    public Color32 EyeColor;
    public byte SkinColor;

    public bool IsMale
    {
        get => (_shoes & 0x80) != 0;
        set
        {
            if (value)
                _shoes |= 0x80;
            else
                _shoes &= 0x7F;
        }
    }

    public byte Shoes
    {
        get => (byte)(_shoes & 0x7F);
        set => _shoes = (byte)((_shoes & 0x80) | (value & 0x7F));
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
        byte[] data = new byte[17];
        data[0] = Hat;
        data[1] = Hair;
        data[2] = Face;
        data[3] = Eyes;
        data[4] = Shirt;
        data[5] = Overwear;
        data[6] = Gloves;
        data[7] = Pants;
        data[8] = Socks;
        data[9] = _shoes;
        data[10] = HairColor.r;
        data[11] = HairColor.g;
        data[12] = HairColor.b;
        data[13] = EyeColor.r;
        data[14] = EyeColor.g;
        data[15] = EyeColor.b;
        data[16] = SkinColor;

        return Convert.ToBase64String(data);
    }

    public static PlayerClothesInfo Deserialize(string data)
    {
        byte[] bytes = Convert.FromBase64String(data);

        if (bytes.Length != 17)
            throw new ArgumentException("Invalid data length. Expected 10 bytes.");

        return new PlayerClothesInfo
        {
            Hat = bytes[0],
            Hair = bytes[1],
            Face = bytes[2],
            Eyes = bytes[3],
            Shirt = bytes[4],
            Overwear = bytes[5],
            Gloves = bytes[6],
            Pants = bytes[7],
            Socks = bytes[8],
            _shoes = bytes[9],
            HairColor = new Color32(bytes[10], bytes[11], bytes[12], 255),
            EyeColor = new Color32(bytes[13], bytes[14], bytes[15], 255),
            SkinColor = bytes[16]
        };
    }

    public bool Equals(PlayerClothesInfo other)
    {
        return Hat == other.Hat &&
               Hair == other.Hair &&
               Face == other.Face &&
               Eyes == other.Eyes &&
               Shirt == other.Shirt &&
               Overwear == other.Overwear &&
               Gloves == other.Gloves &&
               Pants == other.Pants &&
               Socks == other.Socks &&
               _shoes == other._shoes &&
               HairColor.Equals(other.HairColor) &&
               EyeColor.Equals(other.EyeColor) &&
               SkinColor == other.SkinColor;
    }
}
