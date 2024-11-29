public struct TrainerCardInfo
{
    public byte BackgroundIndex { get; set; }
    public byte FrameIndex { get; set; }
    public sbyte RotationOffset { get; set; }
    public short TrainerOffestX { get; set; }
    public short TrainerOffestY { get; set; }
    public byte TrainerScale { get; set; }
    public byte TrainerAnimation { get; set; }

    public byte[] Serialize()
    {
        byte[] data = new byte[9];
        data[0] = BackgroundIndex;
        data[1] = FrameIndex;
        data[2] = (byte)RotationOffset;
        data[3] = (byte)(TrainerOffestX & 0xFF);
        data[4] = (byte)((TrainerOffestX >> 8) & 0xFF);
        data[5] = (byte)(TrainerOffestY & 0xFF);
        data[6] = (byte)((TrainerOffestY >> 8) & 0xFF);
        data[7] = TrainerScale;
        data[8] = TrainerAnimation;
        return data;
    }

    public static TrainerCardInfo Deserialize(byte[] data)
    {
        return new TrainerCardInfo
        {
            BackgroundIndex = data[0],
            FrameIndex = data[1],
            RotationOffset = (sbyte)data[2],
            TrainerOffestX = (short)(data[3] | (data[4] << 8)),
            TrainerOffestY = (short)(data[5] | (data[6] << 8)),
            TrainerScale = data[7],
            TrainerAnimation = data[8]
        };
    }
}
